using DD3.CoverScope.Models;
using DD3.CoverScope.Services;
using Xunit;

namespace DD3.CoverScope.Tests;

public partial class CoverageMetricsBuilderTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"coverscope-metrics-{Guid.NewGuid():N}");

    public CoverageMetricsBuilderTests() => Directory.CreateDirectory(directory);

    private DD3.CoverScope.Models.CoverageReport Parse(string xml)
    {
        var path = Path.Combine(directory, "coverage.cobertura.xml");
        File.WriteAllText(path, xml);
        return TestServices.CreateCoverageReportService().Parse(path);
    }

    public void Dispose()
    {
        try { Directory.Delete(directory, recursive: true); }
        catch (IOException) { }
    }
}
