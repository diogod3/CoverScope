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
    public async Task CompleteAsync_RoundTripsUtcManifestAndRelativeArtifacts()
    {
        var store = CreateStore();
        var context = await BeginRunAsync(store, CreateTarget("Sample.sln"));
        var coveragePath = Path.Combine(context.DirectoryPath, "coverage.xml");
        await File.WriteAllTextAsync(coveragePath, "<coverage />");

        var completed = await store.CompleteAsync(
            context,
            CoverageRunStatus.Succeeded,
            [new("coverage", "cobertura", "coverage.xml")]);
        var manifestPath = Path.Combine(context.DirectoryPath, "run.json");
        var persisted = await store.ReadAsync(manifestPath);
        var json = await File.ReadAllTextAsync(manifestPath);

        Assert.Equal(completed.SchemaVersion, persisted.SchemaVersion);
        Assert.Equal(completed.Id, persisted.Id);
        Assert.Equal(completed.Target, persisted.Target);
        Assert.Equal(completed.StartedAt, persisted.StartedAt);
        Assert.Equal(completed.CompletedAt, persisted.CompletedAt);
        Assert.Equal(completed.Status, persisted.Status);
        Assert.Equal(completed.Producer, persisted.Producer);
        Assert.Equal(completed.Artifacts.ToArray(), persisted.Artifacts.ToArray());
        Assert.Equal(CoverageRunStatus.Succeeded, persisted.Status);
        Assert.Equal(StartedAt, persisted.CompletedAt);
        Assert.Equal("coverage.xml", persisted.Artifacts.Single().Path);
        Assert.Contains("\"startedAt\": \"2026-09-05T14:32:15.123+00:00\"", json);
        Assert.Contains("\"completedAt\": \"2026-09-05T14:32:15.123+00:00\"", json);
        Assert.Empty(Directory.EnumerateFiles(context.DirectoryPath, "*.tmp"));
    }

    [Theory]
    [InlineData(CoverageRunStatus.Succeeded)]
    [InlineData(CoverageRunStatus.TestsFailed)]
    [InlineData(CoverageRunStatus.ExecutionFailed)]
    [InlineData(CoverageRunStatus.Cancelled)]
    public async Task CompleteAsync_RoundTripsEveryTerminalStatus(CoverageRunStatus status)
    {
        var store = CreateStore();
        var context = await BeginRunAsync(store, CreateTarget($"{status}.sln"));

        await store.CompleteAsync(context, status, []);
        var persisted = await store.ReadAsync(Path.Combine(context.DirectoryPath, "run.json"));

        Assert.Equal(status, persisted.Status);
        Assert.NotNull(persisted.CompletedAt);
    }
}
