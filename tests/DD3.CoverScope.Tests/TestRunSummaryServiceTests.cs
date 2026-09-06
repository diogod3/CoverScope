using DD3.CoverScope.Models;
using DD3.CoverScope.Services;
using Xunit;

namespace DD3.CoverScope.Tests;

public partial class TestRunSummaryServiceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"coverscope-trx-{Guid.NewGuid():N}");

    public TestRunSummaryServiceTests() => Directory.CreateDirectory(directory);

    private string WriteReport(string name, string xml)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, xml);
        return path;
    }

    public void Dispose()
    {
        try { Directory.Delete(directory, recursive: true); }
        catch (IOException) { }
    }
}
