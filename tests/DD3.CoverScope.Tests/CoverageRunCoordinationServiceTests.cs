using DD3.CoverScope.Services.Orchestrations.CoverageResults;
using DD3.CoverScope.Services.Orchestrations.CoverageLifecycle;
using DD3.CoverScope.Services.Orchestrations.CoverageCollection;
using DD3.CoverScope.Services.Coordinations.CoverageRuns;
using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Models;
using DD3.CoverScope.Models.CoverageTargets;
using DD3.CoverScope.Models.Exceptions;
using DD3.CoverScope.Services;

namespace DD3.CoverScope.Tests;
public partial class CoverageRunCoordinationServiceTests
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"coverscope-rejected-run-{Guid.NewGuid():N}");
    private static CoverageRunCoordinationService CreateRunner() => TestServices.CreateRunner();
    private class RunFixture : ICoverageLifecycleOrchestrationService,
        ICoverageCollectionOrchestrationService, ICoverageResultsOrchestrationService
    {
        private readonly int exitCode;
        private readonly int failed;
        private readonly bool hasCoverage;
        private bool recovered;
        public CoverageRunCoordinationService Runner { get; }
        public List<string> Events { get; } = [];
        public bool CancelCollection { get; set; }
        public bool FailCompletion { get; set; }
        public bool FinalizationWasCancelled { get; private set; }
        public Action? OnCollect { get; set; }
        public RunFixture(int exitCode, int failed, bool hasCoverage)
        {
            this.exitCode = exitCode;
            this.failed = failed;
            this.hasCoverage = hasCoverage;
            Runner = new(this, this, this, new DiagnosticsBroker());
        }
        public ValueTask<CoverageRunContext> PrepareAsync(string path, string baseDirectory, CoverageSettings settings, CancellationToken cancellationToken)
        {
            Events.Add("prepare");
            var started = DateTimeOffset.UtcNow;
            return ValueTask.FromResult(new CoverageRunContext(Path.Combine(Path.GetTempPath(), "coverscope-fake-run"),
                new CoverageRun(2, Guid.CreateVersion7(started), new("Sample", CoverageTargetType.Solution, "Sample.sln"),
                    CoverageRunSettings.From(settings), started, null, CoverageRunStatus.Created, new("CoverScope", "test"), []), Path.Combine(baseDirectory, path)));
        }
        public ValueTask<CoverageRunContext> StartAsync(CoverageRunContext run, CancellationToken cancellationToken)
        {
            Events.Add("start");
            return ValueTask.FromResult(run with { Manifest = run.Manifest with { Status = CoverageRunStatus.Running } });
        }
        public ValueTask<CoverageRunContext> RequestCancellationAsync(CoverageRunContext run, CancellationToken cancellationToken)
        {
            Events.Add("cancel-requested");
            return ValueTask.FromResult(run with { Manifest = run.Manifest with { Status = CoverageRunStatus.CancellationRequested } });
        }
        public ValueTask<CoverageRun> CompleteAsync(CoverageRunContext run, CoverageRunStatus status,
            IReadOnlyList<CoverageRunArtifact> artifacts, CancellationToken cancellationToken)
        {
            Events.Add("complete");
            FinalizationWasCancelled = cancellationToken.IsCancellationRequested;
            if (FailCompletion) throw new CoverageOperationDependencyException("RunStore", new IOException("disk full"));
            return ValueTask.FromResult(run.Manifest with { Status = status, CompletedAt = DateTimeOffset.UtcNow, Artifacts = artifacts });
        }
        public ValueTask<CoverageCollectionResult> CollectAsync(CoverageRunContext run,
            IProgress<CoverageCollectionPhase>? progress, CancellationToken cancellationToken)
        {
            Events.Add("collect");
            OnCollect?.Invoke();
            if (CancelCollection) throw new OperationCanceledException(cancellationToken);
            return ValueTask.FromResult(new CoverageCollectionResult(exitCode, "test output", CoverageArtifacts.Empty));
        }
        public ValueTask<CoverageArtifacts> RetrieveArtifactsAsync(CoverageRunContext run, CancellationToken cancellationToken)
        {
            Events.Add("recover");
            recovered = true;
            return ValueTask.FromResult(CoverageArtifacts.Empty);
        }
        public ValueTask<CoverageResults> ProcessAsync(CoverageRunContext run, CoverageArtifacts artifacts, CancellationToken cancellationToken)
        {
            Events.Add(recovered ? "process-recovery" : "process");
            return ValueTask.FromResult(new CoverageResults(hasCoverage ? Path.Combine(run.DirectoryPath, "coverage.xml") : null,
                new TestRunSummary(1, failed, 0, failed + 1, TimeSpan.Zero, []), hasCoverage ? 1 : 0));
        }
    }

}
