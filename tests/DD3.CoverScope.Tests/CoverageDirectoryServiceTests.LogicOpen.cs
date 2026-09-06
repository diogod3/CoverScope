using DD3.CoverScope;
using DD3.CoverScope.Services.Foundations.CoverageDirectories;
using DD3.CoverScope.Services.Views;
using DD3.CoverScope.Services;
using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Brokers.FileSystems;
using DD3.CoverScope.Services.Foundations.CoverageTargets;
using Xunit;

namespace DD3.CoverScope.Tests;

public partial class CoverageDirectoryServiceTests
{
    [Fact]
    public void Open_StartsFromConfiguredInvocationDirectory()
    {
        var configuredBrowser = CreateBrowser();

        var result = configuredBrowser.Open();

        Assert.Equal(Path.GetFullPath(directory), result.DirectoryPath);
    }
}
