using PersonalWorkBoard.Domain;
using PersonalWorkBoard.Domain.Sync;

namespace PersonalWorkBoard.Domain.Tests;

public sealed class SyncContractTests
{
    [Fact]
    public void Pending_mutation_keeps_the_base_version_as_sync_metadata()
    {
        var mutation = new PendingMutation(Guid.NewGuid(), "WorkItem", Guid.NewGuid(), SyncOperation.Upsert, 7, "{}", DateTimeOffset.UtcNow);
        Assert.Equal(7, mutation.BaseRowVersion);
        Assert.Equal(SyncOperation.Upsert, mutation.Operation);
    }

    [Theory]
    [InlineData(BranchType.CurrentOptimization)]
    [InlineData(BranchType.NextVersion)]
    [InlineData(BranchType.IndependentProject)]
    public void Every_project_branch_is_explicit(BranchType branchType) =>
        Assert.True(Enum.IsDefined(branchType));

    [Fact]
    public void Last_write_wins_when_incoming_timestamp_is_newer()
    {
        var serverTime = new DateTimeOffset(2026, 9, 20, 8, 0, 0, TimeSpan.Zero);
        var phoneTime = serverTime.AddMinutes(3);

        Assert.True(LastWriteWinsPolicy.IncomingWins(phoneTime, serverTime));
        Assert.False(LastWriteWinsPolicy.IncomingWins(serverTime, phoneTime));
        Assert.False(LastWriteWinsPolicy.IncomingWins(serverTime, serverTime));
    }
}
