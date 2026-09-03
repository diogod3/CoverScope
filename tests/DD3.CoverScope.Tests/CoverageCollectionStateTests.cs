using DD3.CoverScope.Models;
using DD3.CoverScope.Services;
using Xunit;

namespace DD3.CoverScope.Tests;

public sealed class CoverageCollectionStateTests
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
    public void Advance_OnlyMovesForwardThroughRealPipelinePhases()
    {
        var state = new CoverageCollectionState();
        state.Start();

        Assert.True(state.Advance(CoverageCollectionPhase.RunningTests));
        Assert.False(state.Advance(CoverageCollectionPhase.RestoringProjects));
        Assert.False(state.Advance(CoverageCollectionPhase.RunningTests));
        Assert.True(state.Advance(CoverageCollectionPhase.ProcessingReports));
        Assert.True(state.Advance(CoverageCollectionPhase.BuildingMetrics));
        Assert.Equal("Building metrics", state.PhaseLabel);
    }

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
