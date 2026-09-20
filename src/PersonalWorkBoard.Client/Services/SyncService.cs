using System.Text.Json;
using System.Text.Json.Serialization;
using PersonalWorkBoard.Client.Models;
using PersonalWorkBoard.Contracts;
using PersonalWorkBoard.Domain;
using PersonalWorkBoard.Domain.Sync;

namespace PersonalWorkBoard.Client.Services;

public sealed record SyncSummary(int Uploaded, int Downloaded, int Conflicts, DateTimeOffset CompletedAt);

public sealed class SyncService(LocalStore store, ApiClient api)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<SyncSummary> SyncNowAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var pending = await store.GetPendingAsync();
            var uploaded = 0;
            var conflicts = 0;
            if (pending.Count > 0)
            {
                var request = new PushChangesRequest(
                    await api.GetDeviceIdAsync(),
                    pending.Select(ToMutation).ToList());
                var pushed = await api.PushAsync(request);
                await store.RemovePendingAsync(pushed.AcceptedMutationIds);
                uploaded = pushed.AcceptedMutationIds.Count;
                conflicts = pushed.Conflicts.Count;
                foreach (var conflict in pushed.Conflicts)
                {
                    await store.SaveConflictAsync(new LocalSyncConflict
                    {
                        MutationId = conflict.MutationId.ToString(),
                        EntityType = conflict.EntityType,
                        EntityId = conflict.EntityId.ToString(),
                        ClientVersion = conflict.ClientVersion,
                        ServerVersion = conflict.ServerVersion,
                        ServerPayloadJson = conflict.ServerPayloadJson,
                        Reason = conflict.Reason
                    });
                }
            }

            var since = long.TryParse(await store.GetStateAsync("last_sequence"), out var sequence) ? sequence : 0;
            var downloaded = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pulled = await api.PullAsync(since);
                foreach (var change in pulled.Changes) await ApplyChangeAsync(change);
                downloaded += pulled.Changes.Count;
                since = pulled.LatestSequence;
                await store.SetStateAsync("last_sequence", since.ToString());
                if (pulled.Changes.Count < 500) break;
            }
            await store.SetStateAsync("last_sync_at", DateTimeOffset.UtcNow.ToString("O"));
            return new SyncSummary(uploaded, downloaded, conflicts, DateTimeOffset.Now);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task QueueWorkItemAsync(LocalWorkItem item)
    {
        item.UpdatedAt = DateTimeOffset.UtcNow.ToString("O");
        await store.UpsertWorkItemAsync(item);
        var dto = ToDto(item);
        await store.EnqueueAsync(new LocalPendingMutation
        {
            MutationId = Guid.NewGuid().ToString(),
            EntityType = "WorkItem",
            EntityId = item.Id,
            Operation = "Upsert",
            BaseRowVersion = item.RowVersion,
            PayloadJson = JsonSerializer.Serialize(dto, JsonOptions),
            ClientChangedAt = DateTimeOffset.UtcNow.ToString("O")
        });
    }

    public async Task QueueTaskAsync(LocalWorkTask task)
    {
        task.UpdatedAt = DateTimeOffset.UtcNow.ToString("O");
        await store.UpsertTaskAsync(task);
        await store.EnqueueAsync(new LocalPendingMutation
        {
            MutationId = Guid.NewGuid().ToString(),
            EntityType = "WorkTask",
            EntityId = task.Id,
            Operation = "Upsert",
            BaseRowVersion = task.RowVersion,
            PayloadJson = JsonSerializer.Serialize(ToDto(task), JsonOptions),
            ClientChangedAt = DateTimeOffset.UtcNow.ToString("O")
        });
    }

    private async Task ApplyChangeAsync(SyncEnvelope change)
    {
        if (change.EntityType == "WorkItem")
        {
            var dto = JsonSerializer.Deserialize<WorkItemDto>(change.PayloadJson, JsonOptions);
            if (dto is not null) await store.UpsertWorkItemAsync(ToLocal(dto));
        }
        else if (change.EntityType == "WorkTask")
        {
            var dto = JsonSerializer.Deserialize<WorkTaskDto>(change.PayloadJson, JsonOptions);
            if (dto is not null) await store.UpsertTaskAsync(ToLocal(dto));
        }
    }

    private static PendingMutation ToMutation(LocalPendingMutation value) => new(
        Guid.Parse(value.MutationId), value.EntityType, Guid.Parse(value.EntityId),
        Enum.Parse<SyncOperation>(value.Operation), value.BaseRowVersion, value.PayloadJson,
        DateTimeOffset.Parse(value.ClientChangedAt));

    private static WorkItemDto ToDto(LocalWorkItem value) => new(
        Guid.Parse(value.Id), ParseNullableGuid(value.ParentId), Enum.Parse<WorkItemType>(value.Type),
        Enum.Parse<WorkItemStatus>(value.Status), Enum.Parse<PriorityLevel>(value.Priority),
        value.Title, value.Description, value.Progress, value.NextAction, ParseDateOnly(value.TargetDate),
        value.CurrentVersion, value.RowVersion, DateTimeOffset.Parse(value.CreatedAt),
        DateTimeOffset.Parse(value.UpdatedAt), ParseDateTimeOffset(value.DeletedAt));

    private static WorkTaskDto ToDto(LocalWorkTask value) => new(
        Guid.Parse(value.Id), ParseNullableGuid(value.WorkItemId), value.Title, value.Detail,
        Enum.Parse<PersonalWorkBoard.Domain.TaskStatus>(value.Status), Enum.Parse<PriorityLevel>(value.Priority), ParseDateOnly(value.PlannedDate),
        ParseDateTimeOffset(value.DueAt), ParseDateTimeOffset(value.ReminderAt), value.EstimatedMinutes,
        value.ActualMinutes, value.Progress, value.RowVersion, DateTimeOffset.Parse(value.CreatedAt),
        DateTimeOffset.Parse(value.UpdatedAt), ParseDateTimeOffset(value.DeletedAt));

    private static LocalWorkItem ToLocal(WorkItemDto value) => new()
    {
        Id = value.Id.ToString(), ParentId = value.ParentId?.ToString(), Type = value.Type.ToString(),
        Status = value.Status.ToString(), Priority = value.Priority.ToString(), Title = value.Title,
        Description = value.Description, Progress = value.Progress, NextAction = value.NextAction,
        TargetDate = value.TargetDate?.ToString("yyyy-MM-dd"), CurrentVersion = value.CurrentVersion,
        RowVersion = value.RowVersion, CreatedAt = value.CreatedAt.ToString("O"), UpdatedAt = value.UpdatedAt.ToString("O"),
        DeletedAt = value.DeletedAt?.ToString("O")
    };

    private static LocalWorkTask ToLocal(WorkTaskDto value) => new()
    {
        Id = value.Id.ToString(), WorkItemId = value.WorkItemId?.ToString(), Title = value.Title,
        Detail = value.Detail, Status = value.Status.ToString(), Priority = value.Priority.ToString(),
        PlannedDate = value.PlannedDate?.ToString("yyyy-MM-dd"), DueAt = value.DueAt?.ToString("O"),
        ReminderAt = value.ReminderAt?.ToString("O"), EstimatedMinutes = value.EstimatedMinutes,
        ActualMinutes = value.ActualMinutes, Progress = value.Progress, RowVersion = value.RowVersion,
        CreatedAt = value.CreatedAt.ToString("O"), UpdatedAt = value.UpdatedAt.ToString("O"),
        DeletedAt = value.DeletedAt?.ToString("O")
    };

    private static Guid? ParseNullableGuid(string? value) => Guid.TryParse(value, out var id) ? id : null;
    private static DateOnly? ParseDateOnly(string? value) => DateOnly.TryParse(value, out var date) ? date : null;
    private static DateTimeOffset? ParseDateTimeOffset(string? value) => DateTimeOffset.TryParse(value, out var date) ? date : null;
}
