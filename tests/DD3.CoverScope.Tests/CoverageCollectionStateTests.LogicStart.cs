using DD3.CoverScope.Models;
using DD3.CoverScope.Services;
using Xunit;

namespace DD3.CoverScope.Tests;

public partial class CoverageCollectionStateTests
{
    [Fact]
    public void Start_BeginsWithPreparingPhaseAndTimestamp()
    {
        var startedAt = new DateTimeOffset(2026, 9, 3, 20, 15, 0, TimeSpan.Zero);
        var state = new CoverageCollectionState();

        state.Start(startedAt);

        Assert.True(state.IsActive);
        Assert.Equal(CoverageCollectionPhase.PreparingCollection, state.Phase);
        Assert.Equal("Preparing collection", state.PhaseLabel);
        Assert.Equal(startedAt, state.StartedAt);
        Assert.Null(state.LastOutcome);
    }

    [Fact]
    public void Start_ResetsACompletedRun()
    {
        var state = new CoverageCollectionState();
        state.Start();
        state.Complete(CoverageRunOutcome.ExecutionFailed);

        state.Start();

        Assert.True(state.IsActive);
        Assert.Equal(CoverageCollectionPhase.PreparingCollection, state.Phase);
        Assert.Null(state.LastOutcome);
    }
}
