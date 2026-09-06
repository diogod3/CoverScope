using CoverageSettings = DD3.CoverScope.Models.CoverageSettings;
using DD3.CoverScope.Services;
using DD3.CoverScope.Services.Orchestrations.CoverageReport;
using DD3.CoverScope.Services.Coordinations.CoverageRuns;
using DD3.CoverScope.Services.Foundations.CoverageSettings;
using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Brokers.Clipboards;
using DD3.CoverScope.Models;
using DD3.CoverScope.Models.Exceptions;
namespace DD3.CoverScope.Services.Views;

public interface ICoverageWorkspaceViewService
{
    Task<CoverageSettings> LoadSettingsAsync();
    Task<CoverageRunResult> RunAsync(string path, string baseDirectory, CoverageSettings settings,
        CancellationToken cancellationToken, IProgress<CoverageCollectionPhase> progress);
    Task<CoverageReportResult> LoadAsync(string path, string? sourceRoot, CancellationToken cancellationToken = default);
    Task<string> ImportAsync(Stream input, CancellationToken cancellationToken = default);
    ValueTask CopyFailureAsync(TestFailure failure);
}
public partial class CoverageWorkspaceViewService(ICoverageRunCoordinationService runner,
    ICoverageReportOrchestrationService reportService, ICoverageSettingsService settingsStore,
    IClipboardBroker clipboardBroker, IDiagnosticsBroker diagnosticsBroker) : ICoverageWorkspaceViewService
{
    public Task<CoverageSettings> LoadSettingsAsync() => Trace(() => TryCatch(async () =>
    {
        try { return await settingsStore.LoadAsync(); }
        catch (CoverageOperationException) { return new CoverageSettings(); }
    })).AsTask();
    public Task<CoverageRunResult> RunAsync(string path, string baseDirectory, CoverageSettings settings,
        CancellationToken cancellationToken, IProgress<CoverageCollectionPhase> progress) =>
        Trace(() => TryCatch(async () => await runner.RunAsync(path, baseDirectory, settings, cancellationToken, progress))).AsTask();
    public Task<CoverageReportResult> LoadAsync(string path, string? sourceRoot, CancellationToken cancellationToken = default) =>
        Trace(() => TryCatch(async () =>
    {
        ValidatePath(path);
        return await reportService.LoadAsync(path, sourceRoot, cancellationToken);
    })).AsTask();
    public Task<string> ImportAsync(Stream input, CancellationToken cancellationToken = default) =>
        Trace(() => TryCatch(async () => await reportService.ImportAsync(input, cancellationToken))).AsTask();
    public async ValueTask CopyFailureAsync(TestFailure failure)
    {
        await Trace(() => TryCatch(async () =>
        {
            ArgumentNullException.ThrowIfNull(failure);
            var location = failure.SourceFile is null ? string.Empty
                : $"{Environment.NewLine}{failure.SourceFile}{(failure.SourceLine is null ? string.Empty : $":{failure.SourceLine}")}";
            var text = $"{failure.FullyQualifiedName}{location}{Environment.NewLine}{failure.Message}{Environment.NewLine}{failure.StackTrace}".Trim();
            await clipboardBroker.CopyAsync(text);
            return true;
        }));
    }
}
