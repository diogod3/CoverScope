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
    public async Task ReadForArtifactAsync_DiscoversManifestAboveNestedCollectorOutput()
    {
        var store = CreateStore();
        var context = await BeginRunAsync(store, CreateTarget("Sample.sln"));
        var nestedDirectory = Path.Combine(context.DirectoryPath, "collector-id");
        Directory.CreateDirectory(nestedDirectory);
        var artifactPath = Path.Combine(nestedDirectory, "coverage.cobertura.xml");
        await File.WriteAllTextAsync(artifactPath, "<coverage />");

        var manifest = await store.ReadForArtifactAsync(artifactPath);

        Assert.NotNull(manifest);
        Assert.Equal(context.Manifest.Id, manifest!.Id);
        Assert.Equal(context.Manifest.Status, manifest.Status);
    }
}
