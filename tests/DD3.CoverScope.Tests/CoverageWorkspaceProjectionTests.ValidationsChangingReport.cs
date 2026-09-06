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
}
