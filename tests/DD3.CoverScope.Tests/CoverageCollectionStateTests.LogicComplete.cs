using DD3.CoverScope.Models;
using DD3.CoverScope.Services;
using Xunit;

namespace DD3.CoverScope.Tests;

public partial class CoverageCollectionStateTests
{
    [Theory]
    [InlineData(CoverageRunOutcome.Succeeded)]
    [InlineData(CoverageRunOutcome.TestsFailed)]
    [InlineData(CoverageRunOutcome.ExecutionFailed)]
    [InlineData(CoverageRunOutcome.Cancelled)]
    public void Complete_AlwaysLeavesLoadingState(CoverageRunOutcome outcome)
    {
        var state = new CoverageCollectionState();
        state.Start();
        state.Advance(CoverageCollectionPhase.RunningTests);

        state.Complete(outcome);

        Assert.False(state.IsActive);
        Assert.Equal(outcome, state.LastOutcome);
        Assert.False(state.Advance(CoverageCollectionPhase.ProcessingReports));
    }
}
