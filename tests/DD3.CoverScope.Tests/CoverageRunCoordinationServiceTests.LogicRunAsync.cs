using DD3.CoverScope.Models;
using Xunit;
namespace DD3.CoverScope.Tests;
public partial class CoverageRunCoordinationServiceTests
{
    [Theory]
    [InlineData(0, 0, true, CoverageRunOutcome.Succeeded)]
    [InlineData(1, 2, true, CoverageRunOutcome.TestsFailed)]
    [InlineData(1, 2, false, CoverageRunOutcome.ExecutionFailed)]
    [InlineData(0, 0, false, CoverageRunOutcome.ExecutionFailed)]
    [InlineData(2, 0, true, CoverageRunOutcome.ExecutionFailed)]
    public async Task RunAsync_ClassifiesCoverageAndTestOutcomes(int exitCode, int failed, bool hasCoverage, CoverageRunOutcome expected)
    {
        var fixture = new RunFixture(exitCode, failed, hasCoverage);
        var result = await fixture.Runner.RunAsync("Sample.sln", Path.GetTempPath(), new());
        Assert.Equal(expected, result.Outcome);
        Assert.Equal(failed, result.Tests?.Failed);
        Assert.Equal(hasCoverage, result.HasCoverage);
        Assert.Equal(new[] { "prepare", "start", "collect", "process", "complete" },
            fixture.Events.Where(item => item != "recover" && item != "process-recovery"));
        Assert.NotNull(result.Run?.CompletedAt);
    }

    [Fact]
    public async Task RunAsync_FinalizesCancellationWithAnIndependentTokenAndRetainsArtifacts()
    {
        var fixture = new RunFixture(0, 1, true) { CancelCollection = true };
        using var cancellation = new CancellationTokenSource();
        fixture.OnCollect = cancellation.Cancel;
        var result = await fixture.Runner.RunAsync("Sample.sln", Path.GetTempPath(), new(), cancellation.Token);
        Assert.Equal(CoverageRunOutcome.Cancelled, result.Outcome);
        Assert.True(result.HasCoverage);
        Assert.Equal(1, result.Tests?.Failed);
        Assert.Equal(CoverageRunStatus.Cancelled, result.Run?.Status);
        Assert.Contains("cancel-requested", fixture.Events);
        Assert.False(fixture.FinalizationWasCancelled);
    }

    [Fact]
    public async Task RunAsync_ReportsManifestFailureWithoutClaimingStoredCompletion()
    {
        var fixture = new RunFixture(0, 0, true) { FailCompletion = true };
        var result = await fixture.Runner.RunAsync("Sample.sln", Path.GetTempPath(), new());
        Assert.Equal(CoverageRunOutcome.ExecutionFailed, result.Outcome);
        Assert.Equal(CoverageRunStatus.Running, result.Run?.Status);
        Assert.Contains("final run manifest could not be saved", result.Message);
        Assert.Contains("disk full", result.Output);
    }
}
