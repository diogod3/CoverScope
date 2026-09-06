using DD3.CoverScope.Services;
using Xunit;

namespace DD3.CoverScope.Tests;

public partial class CoverageReportMergeServiceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"coverscope-merge-{Guid.NewGuid():N}");

    public CoverageReportMergeServiceTests() => Directory.CreateDirectory(directory);

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
