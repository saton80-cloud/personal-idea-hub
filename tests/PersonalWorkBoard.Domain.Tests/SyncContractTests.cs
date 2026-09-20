using PersonalWorkBoard.Domain;
using PersonalWorkBoard.Domain.Sync;

namespace PersonalWorkBoard.Domain.Tests;

public sealed class SyncContractTests
{
    [Fact]
    public void Pending_mutation_keeps_the_base_version_for_conflict_detection()
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
}
