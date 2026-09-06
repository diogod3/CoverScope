using DD3.CoverScope.Models;
using DD3.CoverScope.Services;
using Xunit;

namespace DD3.CoverScope.Tests;

public partial class CoverageCollectionStateTests
{
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
}
