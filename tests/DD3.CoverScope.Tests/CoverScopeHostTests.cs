using DD3.CoverScope;
using Xunit;

namespace DD3.CoverScope.Tests;

public partial class CoverScopeHostTests
{
    [Theory]
    [InlineData("http://127.0.0.1:43127", "http://coverscope.localhost:43127")]
    [InlineData("http://127.0.0.1:65535", "http://coverscope.localhost:65535")]
    public void CreateStartupUrls_PreservesTheLoopbackPort(
        string boundUrl,
        string expectedPreferredUrl)
    {
        var urls = CoverScopeHost.CreateStartupUrls(boundUrl);

        Assert.Equal(expectedPreferredUrl, urls.Preferred);
        Assert.Equal(boundUrl, urls.Fallback);
    }

    [Fact]
    public void AnnounceStartup_PrintsBothUrlsBeforeBrowserLaunchFailure()
    {
        var urls = CoverScopeHost.CreateStartupUrls("http://127.0.0.1:43127");
        using var output = new StringWriter();
        using var error = new StringWriter();
        string? launchedUrl = null;

        CoverScopeHost.AnnounceStartup(
            urls,
            openBrowser: true,
            url =>
            {
                launchedUrl = url;
                throw new InvalidOperationException("forced browser failure");
            },
            output,
            error);

        Assert.Equal(urls.Preferred, launchedUrl);
        Assert.Contains($"CoverScope is running at {urls.Preferred}", output.ToString(), StringComparison.Ordinal);
        Assert.Contains($"Loopback fallback: {urls.Fallback}", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("forced browser failure", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void AnnounceStartup_NoBrowserStillPrintsBothUrls()
    {
        var urls = CoverScopeHost.CreateStartupUrls("http://127.0.0.1:43127");
        using var output = new StringWriter();
        using var error = new StringWriter();
        var browserLaunches = 0;

        CoverScopeHost.AnnounceStartup(
            urls,
            openBrowser: false,
            _ => browserLaunches++,
            output,
            error);

        Assert.Equal(0, browserLaunches);
        Assert.Contains(urls.Preferred, output.ToString(), StringComparison.Ordinal);
        Assert.Contains(urls.Fallback, output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }
}
