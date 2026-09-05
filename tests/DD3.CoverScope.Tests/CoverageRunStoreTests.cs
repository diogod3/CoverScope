using System.Globalization;
using DD3.CoverScope;
using DD3.CoverScope.Models;
using DD3.CoverScope.Services;
using Xunit;

namespace DD3.CoverScope.Tests;

public sealed class CoverageRunStoreTests : IDisposable
{
    private static readonly DateTimeOffset StartedAt = new(2026, 9, 5, 14, 32, 15, 123, TimeSpan.Zero);
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"coverscope-run-tests-{Guid.NewGuid():N}");

    public CoverageRunStoreTests() => Directory.CreateDirectory(directory);

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

        var context = await store.BeginAsync(targetPath);
        var manifestPath = Path.Combine(context.DirectoryPath, "run.json");
        var persisted = await store.ReadAsync(manifestPath);

        Assert.Equal(Path.Combine(directory, ".coverscope", "reports"), Directory.GetParent(context.DirectoryPath)?.FullName);
        Assert.Equal("Sample", persisted.Target.Name);
        Assert.Equal(kind, persisted.Target.Kind);
        Assert.Equal($"Sample{extension}", persisted.Target.RelativePath);
        Assert.Equal(CoverageRunStatus.InProgress, persisted.Status);
        Assert.Null(persisted.CompletedAtUtc);
        Assert.Equal(StartedAt, persisted.StartedAtUtc);
        Assert.Empty(persisted.Artifacts);
    }

    [Fact]
    public async Task CompleteAsync_RoundTripsUtcManifestAndRelativeArtifacts()
    {
        var store = CreateStore();
        var context = await store.BeginAsync(CreateTarget("Sample.sln"));
        var coveragePath = Path.Combine(context.DirectoryPath, "coverage.xml");
        await File.WriteAllTextAsync(coveragePath, "<coverage />");

        var completed = await store.CompleteAsync(
            context,
            CoverageRunStatus.Completed,
            [new("coverage", "cobertura", "coverage.xml")]);
        var manifestPath = Path.Combine(context.DirectoryPath, "run.json");
        var persisted = await store.ReadAsync(manifestPath);
        var json = await File.ReadAllTextAsync(manifestPath);

        Assert.Equal(completed.SchemaVersion, persisted.SchemaVersion);
        Assert.Equal(completed.RunId, persisted.RunId);
        Assert.Equal(completed.Target, persisted.Target);
        Assert.Equal(completed.StartedAtUtc, persisted.StartedAtUtc);
        Assert.Equal(completed.CompletedAtUtc, persisted.CompletedAtUtc);
        Assert.Equal(completed.Status, persisted.Status);
        Assert.Equal(completed.Producer, persisted.Producer);
        Assert.Equal(completed.Artifacts.ToArray(), persisted.Artifacts.ToArray());
        Assert.Equal(CoverageRunStatus.Completed, persisted.Status);
        Assert.Equal(StartedAt, persisted.CompletedAtUtc);
        Assert.Equal("coverage.xml", persisted.Artifacts.Single().Path);
        Assert.Contains("\"startedAtUtc\": \"2026-09-05T14:32:15.123Z\"", json);
        Assert.Contains("\"completedAtUtc\": \"2026-09-05T14:32:15.123Z\"", json);
        Assert.Empty(Directory.EnumerateFiles(context.DirectoryPath, "*.tmp"));
    }

    [Theory]
    [InlineData(CoverageRunStatus.Completed)]
    [InlineData(CoverageRunStatus.CompletedWithTestFailures)]
    [InlineData(CoverageRunStatus.Failed)]
    [InlineData(CoverageRunStatus.Cancelled)]
    public async Task CompleteAsync_RoundTripsEveryTerminalStatus(CoverageRunStatus status)
    {
        var store = CreateStore();
        var context = await store.BeginAsync(CreateTarget($"{status}.sln"));

        await store.CompleteAsync(context, status, []);
        var persisted = await store.ReadAsync(Path.Combine(context.DirectoryPath, "run.json"));

        Assert.Equal(status, persisted.Status);
        Assert.NotNull(persisted.CompletedAtUtc);
    }

    [Theory]
    [InlineData(CoverageRunOutcome.Succeeded, true, CoverageRunStatus.Completed)]
    [InlineData(CoverageRunOutcome.TestsFailed, true, CoverageRunStatus.CompletedWithTestFailures)]
    [InlineData(CoverageRunOutcome.TestsFailed, false, CoverageRunStatus.Failed)]
    [InlineData(CoverageRunOutcome.ExecutionFailed, false, CoverageRunStatus.Failed)]
    [InlineData(CoverageRunOutcome.ExecutionFailed, true, CoverageRunStatus.Failed)]
    [InlineData(CoverageRunOutcome.Cancelled, false, CoverageRunStatus.Cancelled)]
    public void ToStatus_DistinguishesCoverageAndCollectionOutcomes(
        CoverageRunOutcome outcome,
        bool hasCoverage,
        CoverageRunStatus expected)
    {
        Assert.Equal(expected, CoverageRunStore.ToStatus(outcome, hasCoverage));
    }

    [Fact]
    public async Task CompleteAsync_RejectsArtifactTraversal()
    {
        var store = CreateStore();
        var context = await store.BeginAsync(CreateTarget("Sample.sln"));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            store.CompleteAsync(
                context,
                CoverageRunStatus.Completed,
                [new("coverage", "cobertura", $"..{Path.DirectorySeparatorChar}coverage.xml")]));

        Assert.Contains("traversal", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompleteAsync_RejectsMissingArtifact()
    {
        var store = CreateStore();
        var context = await store.BeginAsync(CreateTarget("Sample.sln"));

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            store.CompleteAsync(
                context,
                CoverageRunStatus.Completed,
                [new("coverage", "cobertura", "missing.xml")]));
    }

    [Fact]
    public async Task ReadAsync_RejectsUnsupportedSchemaVersion()
    {
        var store = CreateStore();
        var context = await store.BeginAsync(CreateTarget("Sample.sln"));
        var manifestPath = Path.Combine(context.DirectoryPath, "run.json");
        var json = await File.ReadAllTextAsync(manifestPath);
        await File.WriteAllTextAsync(manifestPath, json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2"));

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() => store.ReadAsync(manifestPath));

        Assert.Contains("version 2", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadAsync_RejectsMalformedManifest()
    {
        var store = CreateStore();
        var context = await store.BeginAsync(CreateTarget("Sample.sln"));
        var manifestPath = Path.Combine(context.DirectoryPath, "run.json");
        await File.WriteAllTextAsync(manifestPath, "{ \"schemaVersion\":");

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(manifestPath));

        Assert.Contains("malformed or incomplete", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadAsync_IgnoresUnknownFutureProperties()
    {
        var store = CreateStore();
        var context = await store.BeginAsync(CreateTarget("Sample.sln"));
        var manifestPath = Path.Combine(context.DirectoryPath, "run.json");
        var json = await File.ReadAllTextAsync(manifestPath);
        await File.WriteAllTextAsync(manifestPath, json.Replace(
            "\"runId\":",
            "\"futureProperty\": { \"enabled\": true },\n  \"runId\":"));

        var manifest = await store.ReadAsync(manifestPath);

        Assert.Equal(context.Manifest.RunId, manifest.RunId);
    }

    [Fact]
    public async Task BeginAsync_RetriesRunIdCollisionWithoutOverwriting()
    {
        var firstGuid = Guid.Parse("a83f2c1d-1111-1111-1111-111111111111");
        var secondGuid = Guid.Parse("b94a3d2e-2222-2222-2222-222222222222");
        var guids = new Queue<Guid>([firstGuid, firstGuid, secondGuid]);
        var store = CreateStore(() => guids.Dequeue());
        var targetPath = CreateTarget("Sample.sln");

        var first = await store.BeginAsync(targetPath);
        var second = await store.BeginAsync(targetPath);

        Assert.NotEqual(first.Manifest.RunId, second.Manifest.RunId);
        Assert.True(File.Exists(Path.Combine(first.DirectoryPath, "run.json")));
        Assert.True(File.Exists(Path.Combine(second.DirectoryPath, "run.json")));
    }

    [Fact]
    public async Task BeginAsync_AllocatesUniqueDirectoriesForConcurrentRuns()
    {
        var store = CreateStore();
        var targetPath = CreateTarget("Sample.sln");

        var runs = await Task.WhenAll(
            Enumerable.Range(0, 16).Select(_ => store.BeginAsync(targetPath)));

        Assert.Equal(runs.Length, runs.Select(run => run.Manifest.RunId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(runs, run => Assert.True(File.Exists(Path.Combine(run.DirectoryPath, "run.json"))));
    }

    [Fact]
    public async Task ReadForArtifactAsync_DiscoversManifestAboveNestedCollectorOutput()
    {
        var store = CreateStore();
        var context = await store.BeginAsync(CreateTarget("Sample.sln"));
        var nestedDirectory = Path.Combine(context.DirectoryPath, "collector-id");
        Directory.CreateDirectory(nestedDirectory);
        var artifactPath = Path.Combine(nestedDirectory, "coverage.cobertura.xml");
        await File.WriteAllTextAsync(artifactPath, "<coverage />");

        var manifest = await store.ReadForArtifactAsync(artifactPath);

        Assert.NotNull(manifest);
        Assert.Equal(context.Manifest.RunId, manifest!.RunId);
        Assert.Equal(context.Manifest.Status, manifest.Status);
    }

    [Fact]
    public void Format_UsesTargetNameAndRequestedLocalTimeWithoutStatus()
    {
        var run = CreateManifest(CoverageRunStatus.CompletedWithTestFailures);
        var timeZone = TimeZoneInfo.CreateCustomTimeZone("UTC plus one", TimeSpan.FromHours(1), "UTC plus one", "UTC plus one");

        var label = CoverageRunLabelFormatter.Format(run, CultureInfo.GetCultureInfo("en-GB"), timeZone);

        Assert.Equal("Sample — Sep 5, 15:32", label);
        Assert.DoesNotContain("failed", label, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Failed tests", CoverageRunLabelFormatter.StatusLabel(run.Status));
    }

    [Fact]
    public void ResolveTargetRoot_MatchesCommandLineTargetResolution()
    {
        var targetPath = CreateTarget("Sample.sln");
        var parsed = CoverScopeCommandLine.Parse(["Sample.sln"], directory);

        Assert.True(parsed.Success);
        Assert.Equal(directory, CoverageRunStore.ResolveTargetRoot(parsed.Options!.TargetPath!));
    }

    private CoverageRunStore CreateStore(Func<Guid>? createGuid = null)
    {
        var timeProvider = new FixedTimeProvider(StartedAt);
        var generator = createGuid is null
            ? new CoverageRunIdGenerator(timeProvider)
            : new CoverageRunIdGenerator(timeProvider, createGuid);
        return new(generator, timeProvider);
    }

    private string CreateTarget(string name)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, string.Empty);
        return path;
    }

    private static CoverageRunManifest CreateManifest(CoverageRunStatus status) => new(
        1,
        "20260905T143215Z-a83f2c1d",
        new("Sample", "solution", "Sample.sln"),
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

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
