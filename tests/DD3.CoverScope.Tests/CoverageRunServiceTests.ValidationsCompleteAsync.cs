using DD3.CoverScope.Services.Foundations.CoverageRuns;
using DD3.CoverScope.Brokers.Identifiers;
using DD3.CoverScope.Models.CoverageTargets;
using DD3.CoverScope.Models.Exceptions;
using System.Globalization;
using DD3.CoverScope;
using DD3.CoverScope.Models;
using DD3.CoverScope.Services;
using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Brokers.FileSystems;
using DD3.CoverScope.Services.Foundations.CoverageTargets;
using Xunit;

namespace DD3.CoverScope.Tests;

public partial class CoverageRunServiceTests
{
    [Fact]
    public async Task CompleteAsync_RejectsArtifactTraversal()
    {
        var store = CreateStore();
        var context = await BeginRunAsync(store, CreateTarget("Sample.sln"));

        var exception = await Assert.ThrowsAsync<CoverageOperationValidationException>(() =>
            store.CompleteAsync(
                context,
                CoverageRunStatus.Succeeded,
                [new("coverage", "cobertura", $"..{Path.DirectorySeparatorChar}coverage.xml")]));

        Assert.Contains("traversal", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompleteAsync_RejectsMissingArtifact()
    {
        var store = CreateStore();
        var context = await BeginRunAsync(store, CreateTarget("Sample.sln"));

        await Assert.ThrowsAsync<CoverageOperationDependencyException>(() =>
            store.CompleteAsync(
                context,
                CoverageRunStatus.Succeeded,
                [new("coverage", "cobertura", "missing.xml")]));
    }
}
