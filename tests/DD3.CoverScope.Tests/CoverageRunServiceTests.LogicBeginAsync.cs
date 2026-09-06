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
    [Theory]
    [InlineData(".sln", "solution")]
    [InlineData(".slnx", "solution")]
    [InlineData(".csproj", "project")]
    [InlineData(".fsproj", "project")]
    [InlineData(".vbproj", "project")]
    public async Task BeginAsync_UsesTargetDirectoryAndPersistsInProgressManifest(string extension, string kind)
    {
        var targetPath = CreateTarget($"Sample{extension}");
        var store = CreateStore();

        var context = await BeginRunAsync(store, targetPath);
        var manifestPath = Path.Combine(context.DirectoryPath, "run.json");
        var persisted = await store.ReadAsync(manifestPath);

        Assert.Equal(Path.Combine(directory, ".coverscope", "reports"), Directory.GetParent(context.DirectoryPath)?.FullName);
        Assert.Equal("Sample", persisted.Target.Name);
        Assert.Equal(kind, persisted.Target.Type.ToString().ToLowerInvariant());
        Assert.Equal($"Sample{extension}", persisted.Target.RelativePath);
        Assert.Equal(CoverageRunStatus.Created, persisted.Status);
        Assert.Null(persisted.CompletedAt);
        Assert.Equal(StartedAt, persisted.StartedAt);
        Assert.Empty(persisted.Artifacts);
    }

    [Fact]
    public async Task BeginAsync_RetriesRunIdCollisionWithoutOverwriting()
    {
        var firstGuid = Guid.CreateVersion7(StartedAt);
        var secondGuid = Guid.CreateVersion7(StartedAt);
        var guids = new Queue<Guid>([firstGuid, firstGuid, secondGuid]);
        var store = CreateStore(() => guids.Dequeue());
        var targetPath = CreateTarget("Sample.sln");

        var first = await BeginRunAsync(store, targetPath);
        var second = await BeginRunAsync(store, targetPath);

        Assert.NotEqual(first.Manifest.Id, second.Manifest.Id);
        Assert.True(File.Exists(Path.Combine(first.DirectoryPath, "run.json")));
        Assert.True(File.Exists(Path.Combine(second.DirectoryPath, "run.json")));
    }

    [Fact]
    public async Task BeginAsync_AllocatesUniqueDirectoriesForConcurrentRuns()
    {
        var store = CreateStore();
        var targetPath = CreateTarget("Sample.sln");

        var runs = await Task.WhenAll(
            Enumerable.Range(0, 16).Select(_ => BeginRunAsync(store, targetPath)));

        Assert.Equal(runs.Length, runs.Select(run => run.Manifest.Id).Distinct().Count());
        Assert.All(runs, run => Assert.True(File.Exists(Path.Combine(run.DirectoryPath, "run.json"))));
    }
}
