using CoverageResults = DD3.CoverScope.Models.CoverageResults;
using CoverageArtifacts = DD3.CoverScope.Models.CoverageArtifacts;
using DD3.CoverScope.Services.Foundations.TestRunSummaries;
using DD3.CoverScope.Services.Foundations.CoverageReports;
using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Models;
using DD3.CoverScope.Models.Exceptions;

namespace DD3.CoverScope.Services.Orchestrations.CoverageResults;

public interface ICoverageResultsOrchestrationService
{
    ValueTask<CoverageResults> ProcessAsync(CoverageRunContext run, CoverageArtifacts artifacts, CancellationToken cancellationToken = default);
}

public partial class CoverageResultsOrchestrationService(
    ICoverageReportMergeService merger, ITestRunSummaryService testParser,
    ICoverageReportService coverageParser, IDiagnosticsBroker diagnosticsBroker) : ICoverageResultsOrchestrationService
{
    public ValueTask<CoverageResults> ProcessAsync(CoverageRunContext run, CoverageArtifacts artifacts,
        CancellationToken cancellationToken = default) => Trace(() => TryCatch(() =>
    {
        ValidateArtifacts(artifacts);
        cancellationToken.ThrowIfCancellationRequested();
        // Keep valid reports and test failures even if cancellation left another collector file incomplete.
        var validReports = new List<string>();
        foreach (var path in artifacts.CoveragePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { coverageParser.ValidateReport(path); validReports.Add(path); }
            catch (CoverageOperationValidationException) { }
        }
        var report = validReports.Count switch
        {
            0 => null,
            1 => validReports[0],
            _ => merger.Merge(validReports, Path.Combine(run.DirectoryPath, "coverage.merged.cobertura.xml"))
        };
        var validTestPaths = new List<string>();
        foreach (var path in artifacts.TestResultPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { testParser.Parse([path]); validTestPaths.Add(path); }
            catch (CoverageOperationValidationException) { }
        }
        var tests = validTestPaths.Count == 0 ? null : testParser.Parse(validTestPaths);
        return ValueTask.FromResult(new CoverageResults(report, tests, validReports.Count));
    }));
}
