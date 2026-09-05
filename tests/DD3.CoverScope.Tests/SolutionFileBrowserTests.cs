using DD3.CoverScope.Services;
using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Brokers.FileSystems;
using DD3.CoverScope.Services.Foundations.CoverageTargets;
using Xunit;

namespace DD3.CoverScope.Tests;

public sealed class SolutionFileBrowserTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"coverscope-browser-{Guid.NewGuid():N}");
    private readonly SolutionFileBrowser browser;

    public SolutionFileBrowserTests()
    {
        Directory.CreateDirectory(directory);
        browser = CreateBrowser();
    }

    private SolutionFileBrowser CreateBrowser()
    {
        var fileSystemBroker = new FileSystemBroker();
        var targetService = new CoverageTargetService(fileSystemBroker, new DiagnosticsBroker());
        return new SolutionFileBrowser(directory, fileSystemBroker, targetService);
    }

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

    [Fact]
    public async Task ValidateSelection_RejectsUnsupportedFiles()
    {
        var path = Path.Combine(directory, "Demo.txt");
        File.WriteAllText(path, string.Empty);

        var result = await browser.ValidateSelectionAsync(path);

        Assert.False(result.Success);
        Assert.Null(result.SelectedPath);
    }

    [Fact]
    public void Open_StartsFromConfiguredInvocationDirectory()
    {
        var configuredBrowser = CreateBrowser();

        var result = configuredBrowser.Open();

        Assert.Equal(Path.GetFullPath(directory), result.DirectoryPath);
    }

    public void Dispose()
    {
        try { Directory.Delete(directory, recursive: true); }
        catch (IOException) { }
    }
}
