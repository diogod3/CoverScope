using DD3.CoverScope.Services;
using Xunit;

namespace DD3.CoverScope.Tests;

public sealed class CoverageMetricsBuilderTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"coverscope-metrics-{Guid.NewGuid():N}");

    public CoverageMetricsBuilderTests() => Directory.CreateDirectory(directory);

    [Fact]
    public void BuildClasses_AggregatesPartialClassFragmentsWithoutMergingSameLineNumbersAcrossFiles()
    {
        var report = Parse("""
            <coverage><packages><package name="Demo"><classes>
              <class name="Demo.PartialWorker" filename="PartialWorker.Commands.cs"><methods>
                <method name="Execute" signature="()"><lines><line number="10" hits="2" /></lines></method>
              </methods><lines><line number="10" hits="2" /></lines></class>
              <class name="Demo.PartialWorker" filename="PartialWorker.Queries.cs"><methods>
                <method name="Query" signature="()"><lines><line number="10" hits="0" /></lines></method>
              </methods><lines><line number="10" hits="0" /></lines></class>
            </classes></package></packages></coverage>
            """);
        var project = report.Packages.Single();
        var namespaceItem = project.Namespaces.Single();

        var row = new CoverageMetricsBuilder().BuildClasses(project, namespaceItem).Single();

        Assert.Equal(1, row.Lines.Covered);
        Assert.Equal(2, row.Lines.Total);
        Assert.Equal(1, row.Methods.Covered);
        Assert.Equal(2, row.Methods.Total);
        Assert.Equal(2, row.FileCount);
        Assert.Equal(2, row.ClassFragments.Count);
    }

    [Fact]
    public void BuildNamespaces_DoesNotDoubleCountAFileLineSharedByMultipleClasses()
    {
        var report = Parse("""
            <coverage><packages><package name="Demo"><classes>
              <class name="Demo.First" filename="Shared.cs"><lines><line number="4" hits="0" /></lines></class>
              <class name="Demo.Second" filename="Shared.cs"><lines><line number="4" hits="3" /><line number="5" hits="0" /></lines></class>
            </classes></package></packages></coverage>
            """);
        var project = report.Packages.Single();

        var row = new CoverageMetricsBuilder().BuildNamespaces(project).Single();

        Assert.Equal(1, row.Lines.Covered);
        Assert.Equal(2, row.Lines.Total);
        Assert.Equal(1, row.FileCount);
        Assert.Equal(2, row.ChildCount);
    }

    private DD3.CoverScope.Models.CoverageReport Parse(string xml)
    {
        var path = Path.Combine(directory, "coverage.cobertura.xml");
        File.WriteAllText(path, xml);
        return new CoberturaParser().Parse(path);
    }

    public void Dispose()
    {
        try { Directory.Delete(directory, recursive: true); }
        catch (IOException) { }
    }
}
