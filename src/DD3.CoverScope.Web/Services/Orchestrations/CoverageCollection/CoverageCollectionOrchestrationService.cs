using CoverageArtifacts = DD3.CoverScope.Models.CoverageArtifacts;
using DD3.CoverScope.Services;
using DD3.CoverScope.Services.Foundations.CoverageTestExecutions;
using DD3.CoverScope.Services.Foundations.CoverageArtifacts;
using DD3.CoverScope.Services.Foundations.CoverageRunSettings;
using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Models;

namespace DD3.CoverScope.Services.Orchestrations.CoverageCollection;

public interface ICoverageCollectionOrchestrationService
{
    ValueTask<CoverageCollectionResult> CollectAsync(CoverageRunContext run,
        IProgress<CoverageCollectionPhase>? progress = null, CancellationToken cancellationToken = default);
    ValueTask<CoverageArtifacts> RetrieveArtifactsAsync(CoverageRunContext run, CancellationToken cancellationToken = default);
}

public partial class CoverageCollectionOrchestrationService(
    ICoverageRunSettingsService settingsWriter, ICoverageTestExecutionService executionService,
    ICoverageArtifactService artifactService, IDiagnosticsBroker diagnosticsBroker) : ICoverageCollectionOrchestrationService
{
    public ValueTask<CoverageCollectionResult> CollectAsync(CoverageRunContext run,
        IProgress<CoverageCollectionPhase>? progress = null, CancellationToken cancellationToken = default) =>
        Trace(() => TryCatch(async () =>
    {
        ValidateRun(run);
        cancellationToken.ThrowIfCancellationRequested();
        var settingsPath = settingsWriter.Write(run.Manifest.Settings.ToSettings(),
            Path.Combine(run.DirectoryPath, "coverscope.runsettings"));
        progress?.Report(CoverageCollectionPhase.RunningTests);
        var result = await executionService.ExecuteAsync(run.TargetPath, run.DirectoryPath, settingsPath, cancellationToken);
        progress?.Report(CoverageCollectionPhase.ProcessingReports);
        var artifacts = await artifactService.RetrieveAsync(run.DirectoryPath, cancellationToken);
        return new CoverageCollectionResult(result.ExitCode, result.Output, artifacts);
    }));

    public ValueTask<CoverageArtifacts> RetrieveArtifactsAsync(CoverageRunContext run, CancellationToken cancellationToken = default) =>
        Trace(() => TryCatch(async () =>
    {
        ValidateRun(run);
        return await artifactService.RetrieveAsync(run.DirectoryPath, cancellationToken);
    }));
}
