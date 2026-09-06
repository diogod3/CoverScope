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
    public async Task ReadAsync_RejectsUnsupportedSchemaVersion()
    {
        var store = CreateStore();
        var context = await BeginRunAsync(store, CreateTarget("Sample.sln"));
        var manifestPath = Path.Combine(context.DirectoryPath, "run.json");
        var json = await File.ReadAllTextAsync(manifestPath);
        await File.WriteAllTextAsync(manifestPath, json.Replace("\"schemaVersion\": 2", "\"schemaVersion\": 1"));

        var exception = await Assert.ThrowsAsync<CoverageOperationValidationException>(() => store.ReadAsync(manifestPath));

        Assert.Contains("version 1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadAsync_RejectsMalformedManifest()
    {
        var store = CreateStore();
        var context = await BeginRunAsync(store, CreateTarget("Sample.sln"));
        var manifestPath = Path.Combine(context.DirectoryPath, "run.json");
        await File.WriteAllTextAsync(manifestPath, "{ \"schemaVersion\":");

        var exception = await Assert.ThrowsAsync<CoverageOperationValidationException>(() => store.ReadAsync(manifestPath));

        Assert.Contains("malformed or incomplete", exception.Message, StringComparison.Ordinal);
    }
}
