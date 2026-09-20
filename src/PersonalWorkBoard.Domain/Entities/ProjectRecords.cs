namespace PersonalWorkBoard.Domain.Entities;

public sealed class ProgressRecord : VersionedEntity
{
    public Guid OwnerId { get; set; }
    public Guid WorkItemId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Blocker { get; set; } = string.Empty;
    public string NextAction { get; set; } = string.Empty;
    public int ProgressBefore { get; set; }
    public int ProgressAfter { get; set; }
    public int SpentMinutes { get; set; }
}

public sealed class ChangeRequest : VersionedEntity
{
    public Guid OwnerId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? SourceIdeaId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public BranchType BranchType { get; set; }
    public string Status { get; set; } = "PENDING_ASSESSMENT";
    public string? TargetVersion { get; set; }
}

public sealed class AcceptanceRecord : VersionedEntity
{
    public Guid OwnerId { get; set; }
    public Guid WorkItemId { get; set; }
    public string Phase { get; set; } = string.Empty;
    public AcceptanceResult Result { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string OptimizationsJson { get; set; } = "[]";
    public string FollowUpsJson { get; set; } = "[]";
}
