using DD3.CoverScope;
using Xunit;

namespace DD3.CoverScope.Tests;

public partial class CoverScopeHostTests
{
    [Fact]
    public async Task ShouldRejectMissingTargetBeforeStartingHostAsync()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sln");

        int exitCode = await CoverScopeHost.RunAsync([path, "--no-browser"]);

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task ShouldRejectUnsupportedTargetBeforeStartingHostAsync()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");

        int exitCode = await CoverScopeHost.RunAsync([path, "--no-browser"]);

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task ShouldRejectMalformedTargetBeforeStartingHostAsync()
    {
        int exitCode = await CoverScopeHost.RunAsync(["invalid\0.sln", "--no-browser"]);

        Assert.Equal(2, exitCode);
    }
}
