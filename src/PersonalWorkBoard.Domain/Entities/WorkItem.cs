namespace PersonalWorkBoard.Domain.Entities;

public sealed class WorkItem : VersionedEntity
{
    public Guid OwnerId { get; set; }
    public Guid? ParentId { get; set; }
    public WorkItemType Type { get; set; }
    public WorkItemStatus Status { get; set; }
    public PriorityLevel Priority { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Progress { get; set; }
    public string NextAction { get; set; } = string.Empty;
    public DateOnly? TargetDate { get; set; }
    public string CurrentVersion { get; set; } = "V0.1";
}
