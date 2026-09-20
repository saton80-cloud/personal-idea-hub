using PersonalWorkBoard.Domain.Sync;

namespace PersonalWorkBoard.Contracts;

public sealed record PullChangesResponse(long LatestSequence, IReadOnlyList<SyncEnvelope> Changes);
public sealed record PushChangesRequest(Guid DeviceId, IReadOnlyList<PendingMutation> Mutations);
public sealed record PushChangesResponse(
    long LatestSequence,
    IReadOnlyList<Guid> AcceptedMutationIds,
    IReadOnlyList<SyncConflict> Conflicts);

public sealed record HealthResponse(string Status, string ServerName, DateTimeOffset ServerTime, string ApiVersion);
