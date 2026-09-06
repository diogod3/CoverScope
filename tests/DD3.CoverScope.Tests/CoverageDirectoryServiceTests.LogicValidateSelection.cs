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
    [Theory]
    [InlineData("Demo.sln")]
    [InlineData("Demo.slnx")]
    [InlineData("Demo.csproj")]
    [InlineData("Demo.fsproj")]
    [InlineData("Demo.vbproj")]
    public async Task ValidateSelection_AcceptsSupportedExistingFiles(string filename)
    {
        var path = Path.Combine(directory, filename);
        File.WriteAllText(path, string.Empty);

        var result = await browser.ValidateSelectionAsync(path);

        Assert.True(result.Success);
        Assert.Equal(Path.GetFullPath(path), result.SelectedPath);
    }
}
