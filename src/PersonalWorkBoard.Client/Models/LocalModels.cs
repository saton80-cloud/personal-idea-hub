using SQLite;

namespace PersonalWorkBoard.Client.Models;

[Table("work_items")]
public sealed class LocalWorkItem
{
    [PrimaryKey] public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? ParentId { get; set; }
    public string Type { get; set; } = "General";
    public string Status { get; set; } = "Inbox";
    public string Priority { get; set; } = "Medium";
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Progress { get; set; }
    public string NextAction { get; set; } = string.Empty;
    public string? TargetDate { get; set; }
    public string CurrentVersion { get; set; } = "V0.1";
    public long RowVersion { get; set; }
    public string CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToString("O");
    public string UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToString("O");
    public string? DeletedAt { get; set; }
}

[Table("work_tasks")]
public sealed class LocalWorkTask
{
    [PrimaryKey] public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? WorkItemId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string Status { get; set; } = "Todo";
    public string Priority { get; set; } = "Medium";
    public string? PlannedDate { get; set; }
    public string? DueAt { get; set; }
    public string? ReminderAt { get; set; }
    public int EstimatedMinutes { get; set; } = 30;
    public int ActualMinutes { get; set; }
    public int Progress { get; set; }
    public long RowVersion { get; set; }
    public string CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToString("O");
    public string UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToString("O");
    public string? DeletedAt { get; set; }
}

[Table("pending_mutations")]
public sealed class LocalPendingMutation
{
    [PrimaryKey] public string MutationId { get; set; } = Guid.NewGuid().ToString();
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Operation { get; set; } = "Upsert";
    public long BaseRowVersion { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public string ClientChangedAt { get; set; } = DateTimeOffset.UtcNow.ToString("O");
}

[Table("client_state")]
public sealed class LocalClientState
{
    [PrimaryKey] public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

[Table("sync_conflicts")]
public sealed class LocalSyncConflict
{
    [PrimaryKey] public string MutationId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public long ClientVersion { get; set; }
    public long ServerVersion { get; set; }
    public string ServerPayloadJson { get; set; } = "{}";
    public string Reason { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToString("O");
}
