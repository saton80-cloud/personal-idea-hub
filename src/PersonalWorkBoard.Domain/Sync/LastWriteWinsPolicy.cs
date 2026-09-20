namespace PersonalWorkBoard.Domain.Sync;

public static class LastWriteWinsPolicy
{
    public static bool IncomingWins(DateTimeOffset incomingUpdatedAt, DateTimeOffset serverUpdatedAt) =>
        incomingUpdatedAt.ToUniversalTime() > serverUpdatedAt.ToUniversalTime();
}
