namespace PersonalWorkBoard.Domain.Sync;

public sealed record SyncEnvelope(
    Guid ChangeId,
    long Sequence,
    string EntityType,
    Guid EntityId,
    SyncOperation Operation,
    long RowVersion,
    string PayloadJson,
    DateTimeOffset ChangedAt);

public sealed record PendingMutation(
    Guid MutationId,
    string EntityType,
    Guid EntityId,
    SyncOperation Operation,
    long BaseRowVersion,
    string PayloadJson,
    DateTimeOffset ClientChangedAt);

public sealed record SyncConflict(
    Guid MutationId,
    string EntityType,
    Guid EntityId,
    long ClientVersion,
    long ServerVersion,
    string ServerPayloadJson,
    string Reason);
