using DD3.CoverScope.Models;
using DD3.CoverScope.Models.Exceptions;
using Xunit;
namespace DD3.CoverScope.Tests;
public partial class CoverageRunServiceTests
{
    [Fact]
    public async Task ChangeStatusAsync_PersistsRunningAndCancellationRequest()
    {
        var store = CreateStore();
        var run = await BeginRunAsync(store, CreateTarget("states.sln"));
        Assert.Equal(7, run.Manifest.Id.Version);
        run = await store.ChangeStatusAsync(run, CoverageRunStatus.Running);
        run = await store.ChangeStatusAsync(run, CoverageRunStatus.CancellationRequested);
        var persisted = await store.ReadAsync(Path.Combine(run.DirectoryPath, "run.json"));
        Assert.Equal(CoverageRunStatus.CancellationRequested, persisted.Status);
        Assert.Null(persisted.CompletedAt);
        Assert.Equal(run.Manifest.Settings, persisted.Settings);
    }
    [Fact]
    public async Task ChangeStatusAsync_RejectsMovingBackToCreated()
    {
        var store = CreateStore();
        var run = await BeginRunAsync(store, CreateTarget("invalid-state.sln"));
        await Assert.ThrowsAsync<CoverageOperationValidationException>(() => store.ChangeStatusAsync(run, CoverageRunStatus.Created));
    }
    [Fact]
    public async Task ReadForArtifactAsync_IgnoresOldAdjacentManifestWithoutChangingIt()
    {
        var store = CreateStore();
        var run = await BeginRunAsync(store, CreateTarget("old-schema.sln"));
        var path = Path.Combine(run.DirectoryPath, "run.json");
        const string oldManifest = "{\"schemaVersion\":1,\"runId\":\"old-id\",\"status\":\"completed\"}";
        await File.WriteAllTextAsync(path, oldManifest);
        var metadata = await store.ReadForArtifactAsync(Path.Combine(run.DirectoryPath, "coverage.cobertura.xml"));
        Assert.Null(metadata);
        Assert.Equal(oldManifest, await File.ReadAllTextAsync(path));
    }
}
