using DD3.CoverScope.Services;
using Xunit;

namespace DD3.CoverScope.Tests;

public sealed class CoberturaReportMergerTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"coverscope-merge-{Guid.NewGuid():N}");

    public CoberturaReportMergerTests() => Directory.CreateDirectory(directory);

    [Fact]
    public void Merge_CombinesPackagesFromAllReports()
    {
        var first = Write("first.xml", Report("One", "One.cs"));
        var second = Write("second.xml", Report("Two", "Two.cs"));
        var merged = Path.Combine(directory, "merged.xml");

        new CoberturaReportMerger().Merge([first, second], merged);
        var result = new CoberturaParser().Parse(merged);

        Assert.Equal(2, result.Packages.Count);
        Assert.Equal(2, result.Files.Count);
        Assert.Contains(result.Packages, x => x.Name == "One");
        Assert.Contains(result.Packages, x => x.Name == "Two");
    }

    private string Write(string name, string content)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static string Report(string package, string file) => $"""
        <coverage><packages><package name="{package}"><classes>
          <class name="{package}" filename="{file}"><lines><line number="1" hits="1" /></lines></class>
        </classes></package></packages></coverage>
        """;

    public void Dispose()
    {
        try { Directory.Delete(directory, recursive: true); }
        catch (IOException) { }
    }
}
