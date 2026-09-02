using DD3.CoverScope.Services;
using Xunit;

namespace DD3.CoverScope.Tests;

public sealed class SolutionFileBrowserTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"coverscope-browser-{Guid.NewGuid():N}");
    private readonly SolutionFileBrowser browser = new();

    public SolutionFileBrowserTests() => Directory.CreateDirectory(directory);

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
    public void ValidateSelection_AcceptsSupportedExistingFiles(string filename)
    {
        var path = Path.Combine(directory, filename);
        File.WriteAllText(path, string.Empty);

        var result = browser.ValidateSelection(path);

        Assert.True(result.Success);
        Assert.Equal(Path.GetFullPath(path), result.SelectedPath);
    }

    [Fact]
    public void ValidateSelection_RejectsUnsupportedFiles()
    {
        var path = Path.Combine(directory, "Demo.txt");
        File.WriteAllText(path, string.Empty);

        var result = browser.ValidateSelection(path);

        Assert.False(result.Success);
        Assert.Null(result.SelectedPath);
    }

    [Fact]
    public void Open_StartsFromConfiguredInvocationDirectory()
    {
        var configuredBrowser = new SolutionFileBrowser(directory);

        var result = configuredBrowser.Open();

        Assert.Equal(Path.GetFullPath(directory), result.DirectoryPath);
    }

    public void Dispose()
    {
        try { Directory.Delete(directory, recursive: true); }
        catch (IOException) { }
    }
}
