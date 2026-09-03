using DD3.CoverScope;
using DD3.CoverScope.Services;
using Xunit;

namespace DD3.CoverScope.Tests;

public sealed class StartupCoverageCoordinatorTests
{
    [Fact]
    public void TryBegin_ClaimsExplicitTargetExactlyOnce()
    {
        var target = Path.GetFullPath("Demo.sln");
        var coordinator = new StartupCoverageCoordinator(
            new CoverScopeLaunchContext(Path.GetTempPath(), target));

        Assert.True(coordinator.TryBegin(out var claimedTarget));
        Assert.Equal(target, claimedTarget);
        Assert.False(coordinator.TryBegin(out var repeatedTarget));
        Assert.Equal(target, repeatedTarget);
    }

    [Fact]
    public void TryBegin_DoesNotClaimLaunchWithoutExplicitTarget()
    {
        var coordinator = new StartupCoverageCoordinator(
            new CoverScopeLaunchContext(Path.GetTempPath(), null));

        Assert.False(coordinator.TryBegin(out var target));
        Assert.Null(target);
        Assert.False(coordinator.TryBegin(out target));
    }

    [Fact]
    public async Task TryBegin_IsThreadSafeAcrossSimultaneousCircuits()
    {
        var coordinator = new StartupCoverageCoordinator(
            new CoverScopeLaunchContext(Path.GetTempPath(), Path.GetFullPath("Demo.sln")));
        using var gate = new ManualResetEventSlim();
        var attempts = Enumerable.Range(0, 32)
            .Select(index => Task.Run(() =>
            {
                gate.Wait();
                return coordinator.TryBegin(out _);
            }))
            .ToArray();

        gate.Set();
        var results = await Task.WhenAll(attempts);

        Assert.Single(results, result => result);
    }
}
