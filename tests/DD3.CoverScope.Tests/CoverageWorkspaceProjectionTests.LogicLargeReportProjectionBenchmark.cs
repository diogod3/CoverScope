using System.Diagnostics;
using System.Text;
using DD3.CoverScope.Models;
using DD3.CoverScope.Services;
using Xunit;
using Xunit.Abstractions;

namespace DD3.CoverScope.Tests;

public partial class CoverageWorkspaceProjectionTests
{
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
        var report = TestServices.CreateCoverageReportService().Parse(reportPath);
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
}
