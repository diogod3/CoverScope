using DD3.CoverScope.Brokers;
using DD3.CoverScope.Models.Reviews;
using DD3.CoverScope.Services.Reviews;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DD3.CoverScope.Tests;

public sealed class ReviewLifecycleTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "coverscope-lifecycle-" + Guid.NewGuid().ToString("N"));
    private string Target => Path.Combine(directory, "Example.csproj");

    public ReviewLifecycleTests()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Target, "<Project />");
    }

    [Fact]
    public async Task CancellationDuringFormattingDiscardsCompletedTestReports()
    {
        var files = new FileSystemBroker();
        var broker = new FixtureProcesses(directory) { PauseFormatting = true };
        var store = new ReviewStore(files, Path.Combine(directory, "runs"));
        var runs = Create(broker, files, store);
        var record = await runs.StartAsync(new(Target, new("refs/heads/main")));
        await broker.FormattingEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.NotEmpty(Directory.GetFiles(store.AnalysisDirectory(record), "*.trx", SearchOption.AllDirectories));

        await runs.CancelAsync(directory);

        var snapshot = await store.ReadAsync(record);
        Assert.Equal(ReviewStatus.Cancelled, snapshot.Record.Status);
        Assert.Null(snapshot.Evidence);
        Assert.False(Directory.Exists(store.AnalysisDirectory(record)));
        Assert.Null(runs.ActiveRecord(directory));
        using var lease = store.Acquire(directory);
    }

    [Fact]
    public async Task InvalidCoverageDoesNotInvalidateUsableTestResults()
    {
        var broker = new FixtureProcesses(directory) { CorruptCoverage = true };
        var service = new VerificationService(broker, new FileSystemBroker(), new ReportReader());
        var evidence = new ReviewEvidence
        {
            Comparison = new(directory, Target, "feature", "head", "refs/heads/main", "target", "base"),
            Settings = new()
        };

        await service.TestsAsync(evidence, Path.Combine(directory, "analysis"), () => { }, CancellationToken.None);

        Assert.Equal(CheckStatus.Completed, evidence.Tests.Status);
        Assert.Equal("Passed", Assert.Single(evidence.Executions).Outcome);
        Assert.False(evidence.Coverage.Available);
        Assert.Contains("could not be read", evidence.Coverage.Limitation ?? "");
    }

    [Fact]
    public async Task FinalPersistenceFailureRemainsVisibleAndKeepsOwnership()
    {
        using var files = new FailingFinalRecordFiles();
        var broker = new FixtureProcesses(directory);
        var store = new ReviewStore(files, Path.Combine(directory, "runs"));
        var runs = Create(broker, files, store);
        var unresolved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        runs.Changed += () =>
        {
            if (runs.ActiveRecord(directory)?.Status == ReviewStatus.CancellationIncomplete) { unresolved.TrySetResult(); }
        };

        var record = await runs.StartAsync(new(Target, new("refs/heads/main", false, false)));
        await unresolved.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(ReviewStatus.CancellationIncomplete, runs.ActiveRecord(directory)?.Status);
        Assert.Contains("finalisation", runs.ActiveRecord(directory)?.OperationalError ?? "");
        Assert.Null((await store.ReadAsync(record)).Evidence);
        Assert.Throws<InvalidOperationException>(() => store.Acquire(directory));
    }

    private static ReviewOrchestrationService Create(IProcessBroker broker, IFileSystemBroker files, ReviewStore store) =>
        new(new(broker, files), new(broker, files, new()), new(broker, files), store, new Lifetime(), NullLogger<ReviewOrchestrationService>.Instance);

    private sealed class Lifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
    }

    private sealed class FixtureProcesses(string root) : IProcessBroker
    {
        public bool PauseFormatting { get; init; }
        public bool CorruptCoverage { get; init; }
        public TaskCompletionSource FormattingEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ProcessResult> ExecuteAsync(ProcessRequest request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var args = request.Arguments.ToArray();
            if (request.Executable == "git")
            {
                var output = args.Contains("--show-toplevel") ? root : args.Contains("--absolute-git-dir") ? Path.Combine(root, ".git")
                    : args.Contains("--abbrev-ref") ? "feature" : args.Contains("for-each-ref") ? "refs/heads/main\n"
                    : args.Contains("merge-base") ? "base\n" : args.Contains("HEAD^{commit}") ? "head\n"
                    : args.Contains("--verify") ? "target\n" : args.Contains("--version") ? "git version fixture" : "";
                if (args.Contains("archive")) { throw new IOException("Baseline archive unavailable in this fixture."); }
                return new(0, output, "");
            }
            if (args[0] == "format" && PauseFormatting)
            {
                FormattingEntered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            if (args[0] == "test")
            {
                var output = args[Array.IndexOf(args, "--results-directory") + 1];
                Directory.CreateDirectory(output);
                await File.WriteAllTextAsync(Path.Combine(output, "fixture.trx"), """
                    <TestRun><Results><UnitTestResult testId="one" executionId="run-one" testName="Example" outcome="Passed" /></Results></TestRun>
                    """, token);
                if (CorruptCoverage) { await File.WriteAllTextAsync(Path.Combine(output, "coverage.json"), "invalid-json", token); }
            }
            if (args.Contains("--internal-index"))
            {
                var output = args[Array.IndexOf(args, "--internal-index") + 3];
                await File.WriteAllTextAsync(output, "{\"complete\":true}", token);
            }
            return new(0, "10.0.100", "");
        }
    }

    private sealed class FailingFinalRecordFiles : IFileSystemBroker, IDisposable
    {
        private readonly FileSystemBroker inner = new();
        private readonly List<IDisposable> leases = [];
        public bool FileExists(string path) => inner.FileExists(path);
        public bool DirectoryExists(string path) => inner.DirectoryExists(path);
        public void CreateDirectory(string path) => inner.CreateDirectory(path);
        public Task<string> ReadAsync(string path, CancellationToken token = default) => inner.ReadAsync(path, token);
        public Task WriteAsync(string path, string text, CancellationToken token = default) => inner.WriteAsync(path, text, token);
        public void Replace(string source, string destination)
        {
            var text = File.ReadAllText(source);
            if (destination.EndsWith("record.json", StringComparison.Ordinal) &&
                (text.Contains("\"Finished\"", StringComparison.Ordinal) || text.Contains("\"Failed\"", StringComparison.Ordinal)))
            { throw new IOException("Simulated final record persistence failure."); }
            inner.Replace(source, destination);
        }
        public void DeleteFile(string path) => inner.DeleteFile(path);
        public void DeleteDirectory(string path) => inner.DeleteDirectory(path);
        public string[] Files(string path, string pattern, SearchOption option = SearchOption.TopDirectoryOnly) => inner.Files(path, pattern, option);
        public IDisposable Lease(string path) { var lease = inner.Lease(path); leases.Add(lease); return lease; }
        public Task ExtractTarAsync(string source, string target, CancellationToken token) => inner.ExtractTarAsync(source, target, token);
        public void Dispose() { foreach (var lease in leases) { lease.Dispose(); } }
    }

    public void Dispose() { if (Directory.Exists(directory)) { Directory.Delete(directory, true); } }
}
