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
}
