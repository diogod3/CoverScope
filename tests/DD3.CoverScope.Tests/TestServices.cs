using DD3.CoverScope.Models;
using DD3.CoverScope.Services.Orchestrations.CoverageResults;
using DD3.CoverScope.Services.Orchestrations.CoverageLifecycle;
using DD3.CoverScope.Services.Orchestrations.CoverageCollection;
using DD3.CoverScope.Services.Coordinations.CoverageRuns;
using DD3.CoverScope.Services.Foundations.CoverageTestExecutions;
using DD3.CoverScope.Services.Foundations.CoverageArtifacts;
using DD3.CoverScope.Services.Foundations.CoverageRunSettings;
using DD3.CoverScope.Services.Foundations.TestRunSummaries;
using DD3.CoverScope.Services.Foundations.CoverageReports;
using DD3.CoverScope.Services.Foundations.CoverageSettings;
using DD3.CoverScope.Services.Foundations.CoverageRuns;
using DD3.CoverScope.Brokers.DateTimes;
using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Brokers.FileSystems;
using DD3.CoverScope.Brokers.Identifiers;
using DD3.CoverScope.Brokers.Serializations;
using DD3.CoverScope.Brokers.Processes;
using DD3.CoverScope.Services;
using DD3.CoverScope.Services.Foundations.CoverageTargets;
namespace DD3.CoverScope.Tests;

internal static class TestServices
{
    internal static CoverageReportService CreateCoverageReportService() => new(new FileSystemBroker(), new DiagnosticsBroker());
    internal static CoverageReportMergeService CreateCoverageReportMergeService() =>
        new(new FileSystemBroker(), new DiagnosticsBroker(), new DateTimeBroker(TimeProvider.System));
    internal static CoverageRunSettingsService CreateCoverageRunSettingsService() => new(new FileSystemBroker(), new DiagnosticsBroker());
    internal static TestRunSummaryService CreateTestRunSummaryService() => new(new FileSystemBroker(), new DiagnosticsBroker());
    internal static CoverageRunService CreateRunStore(TimeProvider? time = null, IIdentifierBroker? identifiers = null) =>
        new(new FileSystemBroker(), new SerializationBroker(), identifiers ?? new IdentifierBroker(),
            new DateTimeBroker(time ?? TimeProvider.System), new DiagnosticsBroker());

    internal static CoverageRunCoordinationService CreateRunner()
    {
        var fs = new FileSystemBroker();
        var diagnostics = new DiagnosticsBroker();
        return new(
            new CoverageLifecycleOrchestrationService(new CoverageTargetService(fs, diagnostics),
                new CoverageSettingsService(fs, new SerializationBroker(), diagnostics), CreateRunStore(), diagnostics),
            new CoverageCollectionOrchestrationService(CreateCoverageRunSettingsService(),
                new CoverageTestExecutionService(new ProcessBroker(), diagnostics),
                new CoverageArtifactService(fs, diagnostics), diagnostics),
            new CoverageResultsOrchestrationService(CreateCoverageReportMergeService(), CreateTestRunSummaryService(),
                CreateCoverageReportService(), diagnostics), diagnostics);
    }
}
