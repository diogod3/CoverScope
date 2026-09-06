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
    public async Task ValidateSelection_RejectsUnsupportedFiles()
    {
        var path = Path.Combine(directory, "Demo.txt");
        File.WriteAllText(path, string.Empty);

        var result = await browser.ValidateSelectionAsync(path);

        Assert.False(result.Success);
        Assert.Null(result.SelectedPath);
    }
}
