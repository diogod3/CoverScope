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

public partial class CoverageRunServiceTests : IDisposable
{
    private static readonly DateTimeOffset StartedAt = new(2026, 9, 5, 14, 32, 15, 123, TimeSpan.Zero);
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"coverscope-run-tests-{Guid.NewGuid():N}");

    public CoverageRunServiceTests() => Directory.CreateDirectory(directory);

    private async Task<CoverageRunContext> BeginRunAsync(CoverageRunService store, string path)
    {
        var targetService = new CoverageTargetService(new FileSystemBroker(), new DiagnosticsBroker());
        var target = await targetService.RetrieveCoverageTargetAsync(path, directory);
        return await store.BeginAsync(target, CoverageRunSettings.From(new CoverageSettings()));
    }

    private CoverageRunService CreateStore(Func<Guid>? createGuid = null) =>
        TestServices.CreateRunStore(new FixedTimeProvider(StartedAt), createGuid is null ? null : new TestIdentifiers(createGuid));
    private class TestIdentifiers(Func<Guid> createGuid) : IIdentifierBroker
    {
        public Guid CreateIdentifier(DateTimeOffset timestamp) => createGuid();
    }

    private string CreateTarget(string name)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, string.Empty);
        return path;
    }

    private static CoverageRun CreateManifest(CoverageRunStatus status) => new(
        2,
        Guid.CreateVersion7(StartedAt),
        new("Sample", CoverageTargetType.Solution, "Sample.sln"),
        CoverageRunSettings.From(new CoverageSettings()),
        StartedAt,
        StartedAt.AddMinutes(1),
        status,
        new("CoverScope", "0.1.0-beta.3"),
        []);

    public void Dispose()
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
