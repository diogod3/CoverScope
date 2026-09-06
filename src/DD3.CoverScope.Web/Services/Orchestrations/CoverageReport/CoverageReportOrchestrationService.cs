using CoverageReport = DD3.CoverScope.Models.CoverageReport;
using DD3.CoverScope.Services;
using DD3.CoverScope.Services.Foundations.CoverageImports;
using DD3.CoverScope.Services.Foundations.CoverageReports;
using DD3.CoverScope.Services.Foundations.CoverageRuns;
using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Models;
namespace DD3.CoverScope.Services.Orchestrations.CoverageReport;

public record CoverageReportResult(CoverageReport Report, CoverageRun? Run);
public interface ICoverageReportOrchestrationService
{
    ValueTask<CoverageReportResult> LoadAsync(string path, string? sourceRoot, CancellationToken cancellationToken = default);
    ValueTask<string> ImportAsync(Stream input, CancellationToken cancellationToken = default);
}
public partial class CoverageReportOrchestrationService(ICoverageReportService parser, ICoverageRunService runStore,
    ICoverageImportService importService, IDiagnosticsBroker diagnosticsBroker) : ICoverageReportOrchestrationService
{
    public ValueTask<CoverageReportResult> LoadAsync(string path, string? sourceRoot,
        CancellationToken cancellationToken = default) => Trace(() => TryCatch(async () =>
    {
        ValidatePath(path);
        cancellationToken.ThrowIfCancellationRequested();
        var report = parser.Parse(path, sourceRoot);
        var run = await runStore.ReadForArtifactAsync(path, cancellationToken);
        if (run is not null) report = report with { Name = CoverageRunLabelFormatter.Format(run), GeneratedAt = run.StartedAt };
        return new CoverageReportResult(report, run);
    }));
    public ValueTask<string> ImportAsync(Stream input, CancellationToken cancellationToken = default) =>
        Trace(() => TryCatch(async () => await importService.ImportAsync(input, cancellationToken)));
}
