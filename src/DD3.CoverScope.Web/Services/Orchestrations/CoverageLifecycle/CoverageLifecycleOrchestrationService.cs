using CoverageRunSettings = DD3.CoverScope.Models.CoverageRunSettings;
using CoverageSettings = DD3.CoverScope.Models.CoverageSettings;
using DD3.CoverScope.Services.Foundations.CoverageSettings;
using DD3.CoverScope.Services.Foundations.CoverageRuns;
using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Models;
using DD3.CoverScope.Services.Foundations.CoverageTargets;

namespace DD3.CoverScope.Services.Orchestrations.CoverageLifecycle;

public interface ICoverageLifecycleOrchestrationService
{
    ValueTask<CoverageRunContext> PrepareAsync(string path, string baseDirectory, CoverageSettings settings, CancellationToken cancellationToken = default);
    ValueTask<CoverageRunContext> StartAsync(CoverageRunContext run, CancellationToken cancellationToken = default);
    ValueTask<CoverageRunContext> RequestCancellationAsync(CoverageRunContext run, CancellationToken cancellationToken = default);
    ValueTask<CoverageRun> CompleteAsync(CoverageRunContext run, CoverageRunStatus status,
        IReadOnlyList<CoverageRunArtifact> artifacts, CancellationToken cancellationToken = default);
}

public partial class CoverageLifecycleOrchestrationService(
    ICoverageTargetService targetService, ICoverageSettingsService settingsStore,
    ICoverageRunService runStore, IDiagnosticsBroker diagnosticsBroker) : ICoverageLifecycleOrchestrationService
{
    public ValueTask<CoverageRunContext> PrepareAsync(string path, string baseDirectory, CoverageSettings settings,
        CancellationToken cancellationToken = default) => Trace(() => TryCatch(async () =>
    {
        ValidateSettings(settings);
        var effectiveSettings = CoverageRunSettings.From(settings);
        var target = await targetService.RetrieveCoverageTargetAsync(path, baseDirectory, cancellationToken);
        await settingsStore.SaveAsync(effectiveSettings.ToSettings(), cancellationToken);
        return await runStore.BeginAsync(target, effectiveSettings, cancellationToken);
    }));

    public ValueTask<CoverageRunContext> StartAsync(CoverageRunContext run, CancellationToken cancellationToken = default) =>
        Trace(() => TryCatch(async () => await runStore.ChangeStatusAsync(run, CoverageRunStatus.Running, cancellationToken)));

    public ValueTask<CoverageRunContext> RequestCancellationAsync(CoverageRunContext run, CancellationToken cancellationToken = default) =>
        Trace(() => TryCatch(async () => await runStore.ChangeStatusAsync(run, CoverageRunStatus.CancellationRequested, cancellationToken)));

    public ValueTask<CoverageRun> CompleteAsync(CoverageRunContext run, CoverageRunStatus status,
        IReadOnlyList<CoverageRunArtifact> artifacts, CancellationToken cancellationToken = default) =>
        Trace(() => TryCatch(async () => await runStore.CompleteAsync(run, status, artifacts, cancellationToken)));
}
