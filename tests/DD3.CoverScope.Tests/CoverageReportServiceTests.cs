using DD3.CoverScope.Services;
using Xunit;

namespace DD3.CoverScope.Tests;

public partial class CoverageReportServiceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"coverscope-{Guid.NewGuid():N}");

    public CoverageReportServiceTests() => Directory.CreateDirectory(directory);

    private string WriteReport(string xml)
    {
        var path = Path.Combine(directory, "coverage.cobertura.xml");
        File.WriteAllText(path, xml);
        return path;
    }

    public void Dispose()
    {
        try { Directory.Delete(directory, recursive: true); }
        catch (IOException) { }
    }
}
