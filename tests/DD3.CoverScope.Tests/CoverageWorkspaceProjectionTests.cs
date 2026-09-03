using System.Diagnostics;
using System.Text;
using DD3.CoverScope.Models;
using DD3.CoverScope.Services;
using Xunit;
using Xunit.Abstractions;

namespace DD3.CoverScope.Tests;

public sealed class CoverageWorkspaceProjectionTests(ITestOutputHelper output) : IDisposable
{
    private readonly string directory =
        Path.Combine(Path.GetTempPath(), $"coverscope-workspace-{Guid.NewGuid():N}");

    [Fact]
    public void ExplorerProjection_IsReusedUntilInputsChange()
    {
        var report = CreateSmallReport();
        var cache = new CoverageWorkspaceProjectionCache(new CoverageMetricsBuilder());

        var first = cache.GetExplorer(
            report,
            string.Empty,
            ExplorerCoverageFilter.All,
            ExplorerSortMode.Lowest);
        var reused = cache.GetExplorer(
            report,
            string.Empty,
            ExplorerCoverageFilter.All,
            ExplorerSortMode.Lowest);
        var filtered = cache.GetExplorer(
            report,
            "Worker",
            ExplorerCoverageFilter.All,
            ExplorerSortMode.Lowest);

        Assert.Same(first, reused);
        Assert.NotSame(first, filtered);
        Assert.Equal(2, cache.ExplorerBuildCount);
    }

    [Fact]
    public void ChangingReport_InvalidatesExplorerAndMetricsProjections()
    {
        var firstReport = CreateSmallReport("First");
        var secondReport = CreateSmallReport("Second");
        var cache = new CoverageWorkspaceProjectionCache(new CoverageMetricsBuilder());

        var firstExplorer = cache.GetExplorer(
            firstReport,
            string.Empty,
            ExplorerCoverageFilter.All,
            ExplorerSortMode.Name);
        var firstMetrics = cache.GetMetrics(firstReport, null, null);
        var reusedMetrics = cache.GetMetrics(firstReport, null, null);
        var secondExplorer = cache.GetExplorer(
            secondReport,
            string.Empty,
            ExplorerCoverageFilter.All,
            ExplorerSortMode.Name);
        var secondMetrics = cache.GetMetrics(secondReport, null, null);

        Assert.Same(firstMetrics, reusedMetrics);
        Assert.NotSame(firstExplorer, secondExplorer);
        Assert.NotSame(firstMetrics, secondMetrics);
        Assert.Equal(2, cache.ExplorerBuildCount);
        Assert.Equal(2, cache.MetricsBuildCount);
    }

    [Fact]
    public void ExplorerProjection_PreservesFilteringSortingAndCounts()
    {
        var report = CreateSmallReport();
        var cache = new CoverageWorkspaceProjectionCache(new CoverageMetricsBuilder());

        var projection = cache.GetExplorer(
            report,
            "Worker",
            ExplorerCoverageFilter.Gaps,
            ExplorerSortMode.Lowest);

        var project = Assert.Single(projection.Projects);
        var namespaceItem = Assert.Single(project.Namespaces);
        var file = Assert.Single(namespaceItem.Files);
        var classItem = Assert.Single(file.Classes);
        Assert.Equal("Demo.Worker", classItem.Class.FullName);
        Assert.Equal(1, projection.FileCount);
        Assert.Equal(1, projection.ClassCount);
    }

    [Fact]
    public void LargeReportProjectionBenchmark_RecordsUncachedAndCacheHitTimings()
    {
        Directory.CreateDirectory(directory);
        var reportPath = Path.Combine(directory, "large.cobertura.xml");
        File.WriteAllText(reportPath, BuildCoberturaXml(
            projectCount: 3,
            namespacesPerProject: 30,
            classesPerNamespace: 10,
            linesPerClass: 50));

        var stopwatch = Stopwatch.StartNew();
        var report = new CoberturaParser().Parse(reportPath);
        stopwatch.Stop();
        var parseMilliseconds = stopwatch.Elapsed.TotalMilliseconds;

        stopwatch.Restart();
        ExplorerProjection? uncached = null;
        for (var index = 0; index < 4; index++)
        {
            uncached = CoverageWorkspaceProjectionCache.BuildExplorerUncached(
                report,
                string.Empty,
                ExplorerCoverageFilter.All,
                ExplorerSortMode.Lowest);
        }
        stopwatch.Stop();
        var fourUncachedMilliseconds = stopwatch.Elapsed.TotalMilliseconds;

        var cache = new CoverageWorkspaceProjectionCache(new CoverageMetricsBuilder());
        stopwatch.Restart();
        var firstExplorer = cache.GetExplorer(
            report,
            string.Empty,
            ExplorerCoverageFilter.All,
            ExplorerSortMode.Lowest);
        stopwatch.Stop();
        var firstExplorerMilliseconds = stopwatch.Elapsed.TotalMilliseconds;

        stopwatch.Restart();
        for (var index = 0; index < 20; index++)
        {
            Assert.Same(
                firstExplorer,
                cache.GetExplorer(
                    report,
                    string.Empty,
                    ExplorerCoverageFilter.All,
                    ExplorerSortMode.Lowest));
        }
        stopwatch.Stop();
        var twentyExplorerHitsMilliseconds = stopwatch.Elapsed.TotalMilliseconds;

        var metrics = new CoverageMetricsBuilder();
        stopwatch.Restart();
        IReadOnlyList<CoverageMetricRow>? uncachedMetrics = null;
        for (var index = 0; index < 3; index++)
            uncachedMetrics = metrics.BuildProjects(report);
        stopwatch.Stop();
        var threeUncachedMetricsMilliseconds = stopwatch.Elapsed.TotalMilliseconds;

        stopwatch.Restart();
        var firstMetrics = cache.GetMetrics(report, null, null);
        stopwatch.Stop();
        var firstMetricsMilliseconds = stopwatch.Elapsed.TotalMilliseconds;

        stopwatch.Restart();
        for (var index = 0; index < 20; index++)
            Assert.Same(firstMetrics, cache.GetMetrics(report, null, null));
        stopwatch.Stop();
        var twentyMetricsHitsMilliseconds = stopwatch.Elapsed.TotalMilliseconds;

        Assert.NotNull(uncached);
        Assert.NotNull(uncachedMetrics);
        Assert.Equal(45_000, report.Lines.Total);
        Assert.Equal(900, firstExplorer.ClassCount);
        Assert.Equal(1, cache.ExplorerBuildCount);
        Assert.Equal(1, cache.MetricsBuildCount);

        output.WriteLine(
            "PERF 45,000 LOC: parse={0:F1} ms; current Explorer render (4 uncached projections)={1:F1} ms; " +
            "first cached Explorer projection={2:F1} ms; 20 Explorer cache hits={3:F3} ms; " +
            "current Metrics render (3 uncached aggregates)={4:F1} ms; first cached Metrics projection={5:F1} ms; " +
            "20 Metrics cache hits={6:F3} ms.",
            parseMilliseconds,
            fourUncachedMilliseconds,
            firstExplorerMilliseconds,
            twentyExplorerHitsMilliseconds,
            threeUncachedMetricsMilliseconds,
            firstMetricsMilliseconds,
            twentyMetricsHitsMilliseconds);
    }

    private CoverageReport CreateSmallReport(string name = "Demo")
    {
        Directory.CreateDirectory(directory);
        var reportPath = Path.Combine(directory, $"{name}.cobertura.xml");
        File.WriteAllText(reportPath, """
            <coverage><packages><package name="Demo"><classes>
              <class name="Demo.Worker" filename="Worker.cs"><methods>
                <method name="Run" signature="()"><lines><line number="1" hits="1" /><line number="2" hits="0" /></lines></method>
              </methods><lines><line number="1" hits="1" /><line number="2" hits="0" /></lines></class>
            </classes></package></packages></coverage>
            """);
        return new CoberturaParser().Parse(reportPath);
    }

    private static string BuildCoberturaXml(
        int projectCount,
        int namespacesPerProject,
        int classesPerNamespace,
        int linesPerClass)
    {
        var xml = new StringBuilder("<coverage timestamp=\"1756944000\"><packages>");
        for (var projectIndex = 0; projectIndex < projectCount; projectIndex++)
        {
            xml.Append("<package name=\"Project").Append(projectIndex).Append("\"><classes>");
            for (var namespaceIndex = 0; namespaceIndex < namespacesPerProject; namespaceIndex++)
            {
                for (var classIndex = 0; classIndex < classesPerNamespace; classIndex++)
                {
                    var typeName = $"Project{projectIndex}.Area{namespaceIndex}.Worker{classIndex}";
                    var fileName = $"Project{projectIndex}/Area{namespaceIndex}/Worker{classIndex}.cs";
                    xml.Append("<class name=\"").Append(typeName)
                        .Append("\" filename=\"").Append(fileName).Append("\"><methods>");
                    for (var methodIndex = 0; methodIndex < 5; methodIndex++)
                    {
                        xml.Append("<method name=\"Method").Append(methodIndex)
                            .Append("\" signature=\"()\"><lines>");
                        var methodStart = methodIndex * (linesPerClass / 5) + 1;
                        var methodEnd = methodIndex == 4
                            ? linesPerClass
                            : methodStart + (linesPerClass / 5) - 1;
                        for (var line = methodStart; line <= methodEnd; line++)
                            AppendLine(xml, line);
                        xml.Append("</lines></method>");
                    }

                    xml.Append("</methods><lines>");
                    for (var line = 1; line <= linesPerClass; line++)
                        AppendLine(xml, line);
                    xml.Append("</lines></class>");
                }
            }
            xml.Append("</classes></package>");
        }
        return xml.Append("</packages></coverage>").ToString();
    }

    private static void AppendLine(StringBuilder xml, int line) =>
        xml.Append("<line number=\"").Append(line)
            .Append("\" hits=\"").Append(line % 4 == 0 ? 0 : 1)
            .Append("\"/>");

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
