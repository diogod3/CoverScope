using Xunit;
namespace DD3.CoverScope.Tests;
public partial class CoverScopeCliExposerTests
{
    [Theory]
    [InlineData("--help")]
    [InlineData("--version")]
    public async Task ExecuteAsync_DoesNotCreateSessionForInformationalOptions(string option)
    {
        Assert.Equal(0, await CreateExposer().ExecuteAsync([option]));
        Assert.Equal(0, sessions.Calls);
        Assert.Equal(0, browser.Calls);
        Assert.NotEmpty(console.Output);
    }
    [Fact]
    public async Task ExecuteAsync_AnnouncesBothUrlsWithoutLaunchingBrowserWhenDisabled()
    {
        Assert.Equal(0, await CreateExposer().ExecuteAsync(["--no-browser"]));
        Assert.Contains(console.Output, line => line.Contains("http://coverscope.localhost:43123"));
        Assert.Contains(console.Output, line => line.Contains("http://127.0.0.1:43123"));
        Assert.Equal(0, browser.Calls);
    }
}
