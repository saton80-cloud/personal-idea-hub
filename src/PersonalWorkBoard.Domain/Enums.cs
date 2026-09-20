namespace PersonalWorkBoard.Domain;

public enum WorkItemType
{
    Code,
    Website,
    ProductIdea,
    NewProduct,
    DailyWork,
    General
}

public enum WorkItemStatus
{
    Inbox,
    Planned,
    InProgress,
    Waiting,
    Done,
    Archived
}

public enum PriorityLevel
{
    Low,
    Medium,
    High,
    Urgent
}

public enum TaskStatus
{
    Todo,
    Doing,
    Blocked,
    Done,
    Cancelled
}

public enum BranchType
{
    CurrentOptimization,
    NextVersion,
    IndependentProject
}

public enum AcceptanceResult
{
    Passed,
    Conditional,
    Rework
}

public enum SyncOperation
{
    Upsert,
    Delete
}
