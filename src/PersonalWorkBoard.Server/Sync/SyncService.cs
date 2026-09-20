using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using PersonalWorkBoard.Contracts;
using PersonalWorkBoard.Domain;
using PersonalWorkBoard.Domain.Sync;
using PersonalWorkBoard.Server.Database;
using PersonalWorkBoard.Server.Security;

namespace PersonalWorkBoard.Server.Sync;

public sealed class SyncService(DbConnectionFactory connections)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<PullChangesResponse> PullAsync(Guid ownerId, long since, int limit, CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 1000);
        await using var connection = connections.Create();
        var rows = (await connection.QueryAsync<SyncEnvelope>(new CommandDefinition(
            """
            SELECT change_id, sequence, entity_type, entity_id, operation, row_version, payload_json, changed_at
            FROM change_feed
            WHERE owner_id = @OwnerId AND sequence > @Since
            ORDER BY sequence
            LIMIT @Limit
            """,
            new { OwnerId = ownerId, Since = Math.Max(0, since), Limit = limit }, cancellationToken: cancellationToken))).AsList();
        var latest = rows.Count == 0 ? Math.Max(0, since) : rows[^1].Sequence;
        return new PullChangesResponse(latest, rows);
    }

    public async Task<PushChangesResponse> PushAsync(AuthPrincipal principal, PushChangesRequest request, CancellationToken cancellationToken)
    {
        if (request.DeviceId != principal.DeviceId) throw new InvalidOperationException("设备身份不匹配。 ");
        var accepted = new List<Guid>();
        var conflicts = new List<SyncConflict>();
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);

        foreach (var mutation in request.Mutations.Take(200))
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            if (await WasAcceptedAsync(connection, transaction, principal.UserId, mutation.MutationId, cancellationToken))
            {
                accepted.Add(mutation.MutationId);
                await transaction.CommitAsync(cancellationToken);
                continue;
            }

            var conflict = mutation.EntityType switch
            {
                "WorkItem" => await ApplyWorkItemAsync(connection, transaction, principal, mutation, cancellationToken),
                "WorkTask" => await ApplyTaskAsync(connection, transaction, principal, mutation, cancellationToken),
                _ => new SyncConflict(mutation.MutationId, mutation.EntityType, mutation.EntityId, mutation.BaseRowVersion, 0, "{}", "不支持的实体类型")
            };

            if (conflict is null) accepted.Add(mutation.MutationId);
            else
            {
                conflicts.Add(conflict);
                await StoreConflictAsync(connection, transaction, principal, mutation, conflict, cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }

        var latest = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT MAX(sequence) FROM change_feed WHERE owner_id = @OwnerId",
            new { OwnerId = principal.UserId }, cancellationToken: cancellationToken)) ?? 0;
        return new PushChangesResponse(latest, accepted, conflicts);
    }

    private static async Task<SyncConflict?> ApplyWorkItemAsync(
        MySqlConnector.MySqlConnection connection,
        MySqlConnector.MySqlTransaction transaction,
        AuthPrincipal principal,
        PendingMutation mutation,
        CancellationToken cancellationToken)
    {
        var incoming = JsonSerializer.Deserialize<WorkItemDto>(mutation.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException("WorkItem内容无效。 ");
        var current = await connection.QuerySingleOrDefaultAsync<WorkItemDto>(new CommandDefinition(
            """
            SELECT id, parent_id, type, status, priority, title, description, progress, next_action,
                   target_date, current_version, row_version, created_at, updated_at, deleted_at
            FROM work_items WHERE id = @Id AND owner_id = @OwnerId FOR UPDATE
            """,
            new { Id = mutation.EntityId, OwnerId = principal.UserId }, transaction, cancellationToken: cancellationToken));

        if (current is not null && current.RowVersion != mutation.BaseRowVersion)
            return Conflict(mutation, current.RowVersion, JsonSerializer.Serialize(current, JsonOptions), "服务器记录已被另一设备更新");
        if (current is null && mutation.BaseRowVersion != 0)
            return Conflict(mutation, 0, "{}", "服务器记录不存在，不能按旧版本更新");

        var now = DateTimeOffset.UtcNow;
        var nextVersion = current is null ? 1 : current.RowVersion + 1;
        var normalized = incoming with
        {
            Id = mutation.EntityId,
            Progress = Math.Clamp(incoming.Progress, 0, 100),
            Title = incoming.Title.Trim()[..Math.Min(200, incoming.Title.Trim().Length)],
            RowVersion = nextVersion,
            CreatedAt = current?.CreatedAt ?? now,
            UpdatedAt = now,
            DeletedAt = mutation.Operation == SyncOperation.Delete ? now : incoming.DeletedAt
        };

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO work_items
              (id, owner_id, parent_id, type, status, priority, title, description, progress, next_action,
               target_date, current_version, row_version, created_at, updated_at, deleted_at)
            VALUES
              (@Id, @OwnerId, @ParentId, @Type, @Status, @Priority, @Title, @Description, @Progress, @NextAction,
               @TargetDate, @CurrentVersion, @RowVersion, @CreatedAt, @UpdatedAt, @DeletedAt)
            ON DUPLICATE KEY UPDATE
              parent_id = VALUES(parent_id), type = VALUES(type), status = VALUES(status), priority = VALUES(priority),
              title = VALUES(title), description = VALUES(description), progress = VALUES(progress),
              next_action = VALUES(next_action), target_date = VALUES(target_date), current_version = VALUES(current_version),
              row_version = VALUES(row_version), updated_at = VALUES(updated_at), deleted_at = VALUES(deleted_at)
            """,
            new
            {
                normalized.Id,
                OwnerId = principal.UserId,
                normalized.ParentId,
                Type = normalized.Type.ToString(),
                Status = normalized.Status.ToString(),
                Priority = normalized.Priority.ToString(),
                normalized.Title,
                normalized.Description,
                normalized.Progress,
                normalized.NextAction,
                normalized.TargetDate,
                normalized.CurrentVersion,
                normalized.RowVersion,
                CreatedAt = normalized.CreatedAt.UtcDateTime,
                UpdatedAt = normalized.UpdatedAt.UtcDateTime,
                DeletedAt = normalized.DeletedAt?.UtcDateTime
            }, transaction, cancellationToken: cancellationToken));
        await AddFeedAsync(connection, transaction, principal, mutation, nextVersion, JsonSerializer.Serialize(normalized, JsonOptions), cancellationToken);
        return null;
    }

    private static async Task<SyncConflict?> ApplyTaskAsync(
        MySqlConnector.MySqlConnection connection,
        MySqlConnector.MySqlTransaction transaction,
        AuthPrincipal principal,
        PendingMutation mutation,
        CancellationToken cancellationToken)
    {
        var incoming = JsonSerializer.Deserialize<WorkTaskDto>(mutation.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException("WorkTask内容无效。 ");
        var current = await connection.QuerySingleOrDefaultAsync<WorkTaskDto>(new CommandDefinition(
            """
            SELECT id, work_item_id, title, detail, status, priority, planned_date, due_at, reminder_at,
                   estimated_minutes, actual_minutes, progress, row_version, created_at, updated_at, deleted_at
            FROM work_tasks WHERE id = @Id AND owner_id = @OwnerId FOR UPDATE
            """,
            new { Id = mutation.EntityId, OwnerId = principal.UserId }, transaction, cancellationToken: cancellationToken));
        if (current is not null && current.RowVersion != mutation.BaseRowVersion)
            return Conflict(mutation, current.RowVersion, JsonSerializer.Serialize(current, JsonOptions), "服务器任务已被另一设备更新");
        if (current is null && mutation.BaseRowVersion != 0)
            return Conflict(mutation, 0, "{}", "服务器任务不存在");

        var now = DateTimeOffset.UtcNow;
        var nextVersion = current is null ? 1 : current.RowVersion + 1;
        var normalized = incoming with
        {
            Id = mutation.EntityId,
            Progress = Math.Clamp(incoming.Progress, 0, 100),
            Title = incoming.Title.Trim()[..Math.Min(200, incoming.Title.Trim().Length)],
            RowVersion = nextVersion,
            CreatedAt = current?.CreatedAt ?? now,
            UpdatedAt = now,
            DeletedAt = mutation.Operation == SyncOperation.Delete ? now : incoming.DeletedAt
        };
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO work_tasks
              (id, owner_id, work_item_id, title, detail, status, priority, planned_date, due_at, reminder_at,
               estimated_minutes, actual_minutes, progress, row_version, created_at, updated_at, deleted_at)
            VALUES
              (@Id, @OwnerId, @WorkItemId, @Title, @Detail, @Status, @Priority, @PlannedDate, @DueAt, @ReminderAt,
               @EstimatedMinutes, @ActualMinutes, @Progress, @RowVersion, @CreatedAt, @UpdatedAt, @DeletedAt)
            ON DUPLICATE KEY UPDATE
              work_item_id = VALUES(work_item_id), title = VALUES(title), detail = VALUES(detail), status = VALUES(status),
              priority = VALUES(priority), planned_date = VALUES(planned_date), due_at = VALUES(due_at),
              reminder_at = VALUES(reminder_at), estimated_minutes = VALUES(estimated_minutes),
              actual_minutes = VALUES(actual_minutes), progress = VALUES(progress), row_version = VALUES(row_version),
              updated_at = VALUES(updated_at), deleted_at = VALUES(deleted_at)
            """,
            new
            {
                normalized.Id,
                OwnerId = principal.UserId,
                normalized.WorkItemId,
                normalized.Title,
                normalized.Detail,
                Status = normalized.Status.ToString(),
                Priority = normalized.Priority.ToString(),
                normalized.PlannedDate,
                DueAt = normalized.DueAt?.UtcDateTime,
                ReminderAt = normalized.ReminderAt?.UtcDateTime,
                normalized.EstimatedMinutes,
                normalized.ActualMinutes,
                normalized.Progress,
                normalized.RowVersion,
                CreatedAt = normalized.CreatedAt.UtcDateTime,
                UpdatedAt = normalized.UpdatedAt.UtcDateTime,
                DeletedAt = normalized.DeletedAt?.UtcDateTime
            }, transaction, cancellationToken: cancellationToken));
        await AddFeedAsync(connection, transaction, principal, mutation, nextVersion, JsonSerializer.Serialize(normalized, JsonOptions), cancellationToken);
        return null;
    }

    private static Task<bool> WasAcceptedAsync(IDbConnection connection, IDbTransaction transaction, Guid ownerId, Guid mutationId, CancellationToken cancellationToken) =>
        connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM change_feed WHERE owner_id = @OwnerId AND mutation_id = @MutationId)",
            new { OwnerId = ownerId, MutationId = mutationId }, transaction, cancellationToken: cancellationToken));

    private static Task AddFeedAsync(IDbConnection connection, IDbTransaction transaction, AuthPrincipal principal, PendingMutation mutation, long rowVersion, string payload, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO change_feed
              (change_id, owner_id, source_device_id, mutation_id, entity_type, entity_id, operation, row_version, payload_json, changed_at)
            VALUES
              (@ChangeId, @OwnerId, @DeviceId, @MutationId, @EntityType, @EntityId, @Operation, @RowVersion, @Payload, @Now)
            """,
            new { ChangeId = Guid.NewGuid(), OwnerId = principal.UserId, DeviceId = principal.DeviceId, mutation.MutationId, mutation.EntityType, mutation.EntityId, Operation = mutation.Operation.ToString(), RowVersion = rowVersion, Payload = payload, Now = DateTime.UtcNow },
            transaction, cancellationToken: cancellationToken));

    private static Task StoreConflictAsync(IDbConnection connection, IDbTransaction transaction, AuthPrincipal principal, PendingMutation mutation, SyncConflict conflict, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO sync_conflicts
              (id, owner_id, device_id, mutation_id, entity_type, entity_id, client_version, server_version,
               client_payload_json, server_payload_json, reason, created_at)
            VALUES
              (@Id, @OwnerId, @DeviceId, @MutationId, @EntityType, @EntityId, @ClientVersion, @ServerVersion,
               @ClientPayload, @ServerPayload, @Reason, @Now)
            """,
            new { Id = Guid.NewGuid(), OwnerId = principal.UserId, DeviceId = principal.DeviceId, mutation.MutationId, mutation.EntityType, mutation.EntityId, ClientVersion = mutation.BaseRowVersion, conflict.ServerVersion, ClientPayload = mutation.PayloadJson, ServerPayload = conflict.ServerPayloadJson, conflict.Reason, Now = DateTime.UtcNow },
            transaction, cancellationToken: cancellationToken));

    private static SyncConflict Conflict(PendingMutation mutation, long serverVersion, string serverPayload, string reason) =>
        new(mutation.MutationId, mutation.EntityType, mutation.EntityId, mutation.BaseRowVersion, serverVersion, serverPayload, reason);
}
