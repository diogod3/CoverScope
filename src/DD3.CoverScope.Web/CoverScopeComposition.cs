using DD3.CoverScope.Models;
using DD3.CoverScope.Services.Orchestrations.CoverScopeSession;
using DD3.CoverScope.Services.Orchestrations.CoverageReport;
using DD3.CoverScope.Services.Orchestrations.CoverageResults;
using DD3.CoverScope.Services.Orchestrations.CoverageLifecycle;
using DD3.CoverScope.Services.Orchestrations.CoverageCollection;
using DD3.CoverScope.Services.Coordinations.CoverageRuns;
using DD3.CoverScope.Services.Foundations.CoverageDirectories;
using DD3.CoverScope.Services.Foundations.CoverScopeSessions;
using DD3.CoverScope.Services.Foundations.CoverageImports;
using DD3.CoverScope.Services.Foundations.CoverageTestExecutions;
using DD3.CoverScope.Services.Foundations.CoverageArtifacts;
using DD3.CoverScope.Services.Foundations.CoverageRunSettings;
using DD3.CoverScope.Services.Foundations.TestRunSummaries;
using DD3.CoverScope.Services.Foundations.CoverageReports;
using DD3.CoverScope.Services.Foundations.CoverageSettings;
using DD3.CoverScope.Services.Foundations.CoverageRuns;
using DD3.CoverScope.Services.Views;
using DD3.CoverScope.Brokers.Clipboards;
using DD3.CoverScope.Brokers.ElementNavigations;
using DD3.CoverScope.Brokers.ApplicationHosts;
using DD3.CoverScope.Brokers.BrowserLaunchers;
using DD3.CoverScope.Brokers.Consoles;
using DD3.CoverScope.Brokers.DateTimes;
using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Brokers.FileSystems;
using DD3.CoverScope.Brokers.Identifiers;
using DD3.CoverScope.Brokers.Processes;
using DD3.CoverScope.Brokers.Serializations;
using DD3.CoverScope.Exposers.Cli;
using DD3.CoverScope.Services;
using DD3.CoverScope.Services.Foundations.CoverageTargets;

namespace DD3.CoverScope;

internal static class CoverScopeComposition
{
    internal static CoverScopeCliExposer CreateCli()
    {
        var diagnostics = new DiagnosticsBroker();
        var targets = new CoverageTargetService(new FileSystemBroker(), diagnostics);
        var sessions = new CoverScopeSessionService(new ApplicationHostBroker(),
            new DateTimeBroker(TimeProvider.System), new IdentifierBroker(), diagnostics);
        return new(new ConsoleBroker(), new BrowserLauncherBroker(),
            new CoverScopeSessionOrchestrationService(targets, sessions, diagnostics), diagnostics);
    }

    internal static void AddBackend(IServiceCollection services, string invocationDirectory)
    {
        services.AddSingleton<IFileSystemBroker, FileSystemBroker>();
        services.AddSingleton<IDiagnosticsBroker, DiagnosticsBroker>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IDateTimeBroker, DateTimeBroker>();
        services.AddSingleton<IIdentifierBroker, IdentifierBroker>();
        services.AddSingleton<ISerializationBroker, SerializationBroker>();
        services.AddSingleton<IProcessBroker, ProcessBroker>();
        services.AddSingleton<ICoverageTargetService, CoverageTargetService>();
        services.AddSingleton<ICoverageReportService, CoverageReportService>();
        services.AddSingleton<ICoverageReportMergeService, CoverageReportMergeService>();
        services.AddSingleton<ITestRunSummaryService, TestRunSummaryService>();
        services.AddSingleton<ICoverageRunSettingsService, CoverageRunSettingsService>();
        services.AddSingleton<ICoverageSettingsService, CoverageSettingsService>();
        services.AddSingleton<ICoverageRunService, CoverageRunService>();
        services.AddSingleton<ICoverageTestExecutionService, CoverageTestExecutionService>();
        services.AddSingleton<ICoverageArtifactService, CoverageArtifactService>();
        services.AddSingleton<ICoverageLifecycleOrchestrationService, CoverageLifecycleOrchestrationService>();
        services.AddSingleton<ICoverageCollectionOrchestrationService, CoverageCollectionOrchestrationService>();
        services.AddSingleton<ICoverageResultsOrchestrationService, CoverageResultsOrchestrationService>();
        services.AddSingleton<ICoverageRunCoordinationService, CoverageRunCoordinationService>();
        services.AddSingleton<ICoverageDirectoryService>(provider => new CoverageDirectoryService(invocationDirectory,
            provider.GetRequiredService<IFileSystemBroker>(), provider.GetRequiredService<IDiagnosticsBroker>()));
        services.AddSingleton<CoverageMetricsBuilder>();
        services.AddScoped<CoverageWorkspaceProjectionCache>();
        services.AddSingleton<ICoverageImportService, CoverageImportService>();
        services.AddSingleton<ICoverageReportOrchestrationService, CoverageReportOrchestrationService>();
        services.AddScoped<IClipboardBroker, ClipboardBroker>();
        services.AddScoped<IElementNavigationBroker, ElementNavigationBroker>();
        services.AddScoped<ICoverageWorkspaceViewService, CoverageWorkspaceViewService>();
        services.AddScoped<ICoverageExplorerWorkspaceViewService, CoverageExplorerWorkspaceViewService>();
        services.AddScoped<ICoverageMetricsWorkspaceViewService, CoverageMetricsWorkspaceViewService>();
        services.AddScoped<ISolutionBrowserViewService, SolutionBrowserViewService>();
    }
}
