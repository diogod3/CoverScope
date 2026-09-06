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
    public void Browse_ListsDirectoriesAndSupportedFilesOnly()
    {
        Directory.CreateDirectory(Path.Combine(directory, "src"));
        File.WriteAllText(Path.Combine(directory, "Demo.sln"), string.Empty);
        File.WriteAllText(Path.Combine(directory, "notes.txt"), string.Empty);

        var result = browser.Browse(directory);

        Assert.Null(result.ErrorMessage);
        Assert.Contains(result.Entries, entry => entry.IsDirectory && entry.Name == "src");
        Assert.Contains(result.Entries, entry => !entry.IsDirectory && entry.Name == "Demo.sln");
        Assert.DoesNotContain(result.Entries, entry => entry.Name == "notes.txt");
    }
}
