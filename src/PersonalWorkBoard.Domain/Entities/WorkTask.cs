namespace PersonalWorkBoard.Domain.Entities;

public sealed class WorkTask : VersionedEntity
{
    public Guid OwnerId { get; set; }
    public Guid? WorkItemId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public TaskStatus Status { get; set; }
    public PriorityLevel Priority { get; set; }
    public DateOnly? PlannedDate { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public DateTimeOffset? ReminderAt { get; set; }
    public int EstimatedMinutes { get; set; } = 30;
    public int ActualMinutes { get; set; }
    public int Progress { get; set; }
}
