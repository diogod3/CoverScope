using DD3.CoverScope.Brokers;
using DD3.CoverScope.Models.Reviews;
using DD3.CoverScope.Services.Reviews;
using Xunit;

namespace DD3.CoverScope.Tests;

public sealed class ReviewCleanupTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "coverscope-cleanup-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemBroker files = new();

    [Fact]
    public async Task CleanupRemovesReadonlyPackagesAndIntermediatesWhilePreservingSavedSourceAndReports()
    {
        var store = new ReviewStore(files, directory);
        var record = new ReviewRecord(Guid.NewGuid().ToString("N"), directory, "App.csproj", "main", DateTimeOffset.UtcNow) { Status = ReviewStatus.Finished };
        var evidence = new ReviewEvidence
        {
            Comparison = new(directory, "App.csproj", "feature", "head", "main", "main", "base"),
            Settings = new(),
            Files = [new("Changed.cs", null, ChangeKind.Modified, 1, 1) { Before = "old source", After = "new source", Diff = "saved patch" }],
            Code = new() { Documents = [new("project", "Unchanged.cs", "baseline document", "Baseline")] }
        };
        await store.SaveRecordAsync(record);
        await store.SaveEvidenceAsync(record, evidence, CancellationToken.None);
        var analysis = store.AnalysisDirectory(record);
        var evidenceBytes = File.ReadAllBytes(Path.Combine(analysis, "evidence.json"));
        var recordBytes = File.ReadAllBytes(Path.Combine(store.RunDirectory(record), "record.json"));
        var packages = Path.Combine(analysis, "baseline-source", "packages", "example");
        Directory.CreateDirectory(packages);
        var package = Path.Combine(packages, "package.dll");
        File.WriteAllText(package, "package");
        File.SetAttributes(package, FileAttributes.ReadOnly);
        // A nested record must never become a history entry.
        File.WriteAllBytes(Path.Combine(packages, "record.json"), recordBytes);
        foreach (var name in new[] { "baseline.tar", "head-index.json", "baseline-index.json", "tests.trx" })
        { File.WriteAllText(Path.Combine(analysis, name), name); }
        Assert.Single(await store.HistoryAsync(directory));

        store.CleanupTemporaryFiles(record);
        store.CleanupTemporaryFiles(record); // Repeating cleanup is harmless.

        Assert.False(Directory.Exists(Path.Combine(analysis, "baseline-source")));
        foreach (var name in new[] { "baseline.tar", "head-index.json", "baseline-index.json" })
        { Assert.False(File.Exists(Path.Combine(analysis, name))); }
        Assert.True(File.Exists(Path.Combine(analysis, "tests.trx")));
        Assert.Equal(evidenceBytes, File.ReadAllBytes(Path.Combine(analysis, "evidence.json")));
        Assert.Equal(recordBytes, File.ReadAllBytes(Path.Combine(store.RunDirectory(record), "record.json")));
        var snapshot = await store.ReadAsync(record);
        Assert.NotNull(snapshot.Evidence);
        Assert.Equal("old source", ReviewPresentation.Source(snapshot.Evidence, "Changed.cs", "Baseline"));
        Assert.Equal("new source", ReviewPresentation.Source(snapshot.Evidence, "Changed.cs", "Head"));
        Assert.Equal("baseline document", ReviewPresentation.Source(snapshot.Evidence, "Unchanged.cs", "Baseline"));
    }

    [Fact]
    public void DirectoryCleanupDoesNotFollowLinksToExternalPackages()
    {
        // Windows link creation can require privileges; readonly cleanup is covered above on every OS.
        if (OperatingSystem.IsWindows()) { return; }
        var baseline = Path.Combine(directory, "baseline-source");
        var external = Path.Combine(directory, "external");
        Directory.CreateDirectory(baseline);
        Directory.CreateDirectory(external);
        var package = Path.Combine(external, "keep.dll");
        File.WriteAllText(package, "keep");
        File.SetAttributes(package, FileAttributes.ReadOnly);
        Directory.CreateSymbolicLink(Path.Combine(baseline, "packages"), external);
        File.CreateSymbolicLink(Path.Combine(baseline, "linked.dll"), package);

        files.DeleteDirectory(baseline);

        Assert.False(Directory.Exists(baseline));
        Assert.Equal("keep", File.ReadAllText(package));
        Assert.True((File.GetAttributes(package) & FileAttributes.ReadOnly) != 0);
    }

    public void Dispose() { if (Directory.Exists(directory)) { files.DeleteDirectory(directory); } }
}
