using PersonalWorkBoard.Domain;

namespace PersonalWorkBoard.Contracts;

public sealed record WorkItemDto(
    Guid Id,
    Guid? ParentId,
    WorkItemType Type,
    WorkItemStatus Status,
    PriorityLevel Priority,
    string Title,
    string Description,
    int Progress,
    string NextAction,
    DateOnly? TargetDate,
    string CurrentVersion,
    long RowVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DeletedAt);

public sealed record WorkTaskDto(
    Guid Id,
    Guid? WorkItemId,
    string Title,
    string Detail,
    TaskStatus Status,
    PriorityLevel Priority,
    DateOnly? PlannedDate,
    DateTimeOffset? DueAt,
    DateTimeOffset? ReminderAt,
    int EstimatedMinutes,
    int ActualMinutes,
    int Progress,
    long RowVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DeletedAt);
