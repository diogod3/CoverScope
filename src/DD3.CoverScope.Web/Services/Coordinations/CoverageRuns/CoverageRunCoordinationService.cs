using CoverageResults = DD3.CoverScope.Models.CoverageResults;
using CoverageArtifacts = DD3.CoverScope.Models.CoverageArtifacts;
using CoverageSettings = DD3.CoverScope.Models.CoverageSettings;
using DD3.CoverScope.Services;
using DD3.CoverScope.Services.Orchestrations.CoverageResults;
using DD3.CoverScope.Services.Orchestrations.CoverageLifecycle;
using DD3.CoverScope.Services.Orchestrations.CoverageCollection;
using DD3.CoverScope.Services.Foundations.CoverageRuns;
using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Brokers.Processes;
using DD3.CoverScope.Models;
using DD3.CoverScope.Models.Exceptions;

namespace DD3.CoverScope.Services.Coordinations.CoverageRuns;

public interface ICoverageRunCoordinationService
{
    Task<CoverageRunResult> RunAsync(string solutionPath, string baseDirectory, CoverageSettings settings,
        CancellationToken cancellationToken = default, IProgress<CoverageCollectionPhase>? progress = null);
}

public partial class CoverageRunCoordinationService(
    ICoverageLifecycleOrchestrationService lifecycleService,
    ICoverageCollectionOrchestrationService collectionService,
    ICoverageResultsOrchestrationService resultsService,
    IDiagnosticsBroker diagnosticsBroker) : ICoverageRunCoordinationService
{
    public Task<CoverageRunResult> RunAsync(string solutionPath, string baseDirectory, CoverageSettings settings,
        CancellationToken cancellationToken = default, IProgress<CoverageCollectionPhase>? progress = null) =>
        Trace(() => TryCatch(async () =>
    {
        ValidateSettings(settings);
        progress?.Report(CoverageCollectionPhase.PreparingCollection);
        CoverageRunContext run;
        try
        {
            run = await lifecycleService.PrepareAsync(solutionPath, baseDirectory, settings, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new CoverageRunResult(CoverageRunOutcome.Cancelled, "The coverage run was cancelled.", string.Empty);
        }
        catch (CoverageOperationException exception)
        {
            return new CoverageRunResult(CoverageRunOutcome.ExecutionFailed, exception.Message, string.Empty);
        }

        CoverageRunOutcome outcome;
        string message;
        string output = string.Empty;
        CoverageArtifacts artifacts = CoverageArtifacts.Empty;
        CoverageResults results = new(null, null, 0);
        try
        {
            run = await lifecycleService.StartAsync(run, cancellationToken);
            var collection = await collectionService.CollectAsync(run, progress, cancellationToken);
            output = collection.Output;
            artifacts = collection.Artifacts;
            results = await resultsService.ProcessAsync(run, artifacts, cancellationToken);
            (outcome, message) = ClassifyResult(collection.ExitCode, results);
        }
        catch (OperationCanceledException exception)
        {
            outcome = CoverageRunOutcome.Cancelled;
            message = "The coverage run was cancelled.";
            if (exception is ProcessCancelledException cancelled) output = cancelled.Result.Output;
            using var finalization = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try { run = await lifecycleService.RequestCancellationAsync(run, finalization.Token); }
            catch (Exception failure) when (failure is CoverageOperationException or OperationCanceledException)
            { output = AppendWarning(output, "Could not record the cancellation request: " + failure.Message); }
        }
        catch (CoverageOperationException exception)
        {
            outcome = CoverageRunOutcome.ExecutionFailed;
            message = exception.InnerException is System.ComponentModel.Win32Exception
                ? ".NET 10 SDK was not found. Install .NET 10 and ensure dotnet is on PATH."
                : "Coverage collection failed.";
            output = AppendWarning(output, exception.Message);
        }

        // Discover output after failure/cancellation too. Finalization has its own bounded token:
        // a cancelled caller must not prevent recording the terminal state.
        using var completion = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        if (outcome is CoverageRunOutcome.ExecutionFailed or CoverageRunOutcome.Cancelled)
        {
            try
            {
                artifacts = await collectionService.RetrieveArtifactsAsync(run, completion.Token);
                results = await resultsService.ProcessAsync(run, artifacts, completion.Token);
            }
            catch (Exception failure) when (failure is CoverageOperationException or OperationCanceledException)
            { output = AppendWarning(output, "Could not recover all run artifacts: " + failure.Message); }
        }

        var storedArtifacts = artifacts.CoveragePaths.Select(path =>
            new CoverageRunArtifact("coverage", "cobertura", Path.GetRelativePath(run.DirectoryPath, path)))
            .Concat(artifacts.TestResultPaths.Select(path =>
                new CoverageRunArtifact("testResults", "trx", Path.GetRelativePath(run.DirectoryPath, path))))
            .ToList();
        if (results.ReportPath is { } report && !artifacts.CoveragePaths.Contains(report))
            storedArtifacts.Add(new("coverage", "cobertura", Path.GetRelativePath(run.DirectoryPath, report)));

        CoverageRun manifest = run.Manifest;
        try
        {
            manifest = await lifecycleService.CompleteAsync(run,
                CoverageRunService.ToStatus(outcome, results.ReportPath is not null), storedArtifacts, completion.Token);
        }
        catch (Exception failure) when (failure is CoverageOperationException or OperationCanceledException)
        {
            message += " The final run manifest could not be saved.";
            output = AppendWarning(output, failure.Message);
            // Preserve the last persisted state instead of claiming completion was stored.
            if (outcome == CoverageRunOutcome.Succeeded) outcome = CoverageRunOutcome.ExecutionFailed;
        }
        return new CoverageRunResult(outcome, message, output, results.ReportPath, results.Tests, manifest);
    })).AsTask();

    private static (CoverageRunOutcome, string) ClassifyResult(int exitCode, CoverageResults results)
    {
        if (results.ReportPath is null)
            return (CoverageRunOutcome.ExecutionFailed, results.Tests?.Failed > 0
                ? $"{results.Tests.Failed} tests failed. No usable coverage report was produced."
                : "No usable Cobertura report was generated. Ensure coverlet.collector is referenced by the test projects.");
        if (results.Tests?.Failed > 0)
            return (CoverageRunOutcome.TestsFailed, $"{results.Tests.Failed} tests failed. Coverage is still available.");
        if (exitCode != 0)
            return (CoverageRunOutcome.ExecutionFailed, "The test process did not complete successfully. Coverage is still available.");
        return (CoverageRunOutcome.Succeeded, $"Coverage collected successfully from {results.ReportCount} report(s).");
    }

    private static string AppendWarning(string output, string warning) =>
        string.IsNullOrEmpty(output) ? warning : output + Environment.NewLine + warning;
}
