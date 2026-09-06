using DD3.CoverScope;
using System.Globalization;
using DD3.CoverScope.Models;
using DD3.CoverScope.Models.Exceptions;
using DD3.CoverScope.Services;
using DD3.CoverScope.Services.Views;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;

namespace DD3.CoverScope.Components.Shared;
public partial class CoverageWorkspace : IDisposable
{
    [Inject] public ICoverageWorkspaceViewService ViewService { get; set; } = default!;
    [Inject] public StartupCoverageCoordinator StartupCoverage { get; set; } = default!;
    [Inject] public CoverScopeLaunchContext LaunchContext { get; set; } = default!;
    [Inject] public IHostApplicationLifetime ApplicationLifetime { get; set; } = default!;
    private bool disposed;
    private CancellationTokenSource runCancellation = new();
    private CoverageMetricRow? metricSelection;
    private TestFailure? failureSelection;
    public void Dispose()
    {
        disposed = true;
        runCancellation.Cancel();
        runCancellation.Dispose();
    }
    private const long MaxReportBytes = 50 * 1024 * 1024;
    private readonly CoverageCollectionState collectionState = new();
    private enum Mode { Run, Import }

    private Mode mode = Mode.Run;
    private WorkspaceMode workspaceMode = WorkspaceMode.Metrics;
    private CoverageSettings settings = new();
    private string solutionPath = string.Empty;
    private string reportPath = string.Empty;
    private string sourceRoot = string.Empty;
    private string? statusMessage;
    private string? runOutput;
    private string? importedReportName;
    private bool statusIsError;
    private CoverageRunOutcome? lastRunOutcome;
    private CoverageRun? currentRun;
    private TestRunSummary? testSummary;
    private bool isBusy;
    private bool isPickingSolution => isSolutionBrowserOpen;
    private bool isSetupOpen;
    private bool isSolutionBrowserOpen;
    private CoverageReport? report;

    protected override async Task OnInitializedAsync()
    {
        runCancellation.Dispose();
        runCancellation = CancellationTokenSource.CreateLinkedTokenSource(ApplicationLifetime.ApplicationStopping);
        settings = await ViewService.LoadSettingsAsync();
        solutionPath = LaunchContext.InitialTargetPath ?? string.Empty;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // OnAfterRenderAsync is not called during static prerendering. The
        // process-scoped claim also prevents refreshes or additional circuits
        // from starting the explicit target more than once.
        if (!firstRender || !StartupCoverage.TryBegin(out var targetPath))
            return;

        solutionPath = targetPath!;
        await RunCoverage();
        await InvokeAsync(StateHasChanged);
    }

    private void ShowMetrics()
    {
        if (report is null) return;
        workspaceMode = WorkspaceMode.Metrics;
        isSetupOpen = false;
    }

    private void ShowExplorer()
    {
        if (report is null) return;
        workspaceMode = WorkspaceMode.Explorer;
        isSetupOpen = false;
    }

    private void ShowRunSetup()
    {
        mode = Mode.Run;
        isSetupOpen = true;
    }

    private void ShowImportSetup()
    {
        mode = Mode.Import;
        isSetupOpen = true;
    }

    private void CloseSetup() => isSetupOpen = false;

    private void OpenStatusDetails()
    {
        if (lastRunOutcome is not null) mode = Mode.Run;
        isSetupOpen = true;
    }

    private Task ChooseSolution()
    {
        isSolutionBrowserOpen = true;
        return Task.CompletedTask;
    }











    private void CloseSolutionBrowser() => isSolutionBrowserOpen = false;

    private async Task RunCoverage()
    {
        if (isBusy || isPickingSolution || string.IsNullOrWhiteSpace(solutionPath))
            return;

        isBusy = true;
        mode = Mode.Run;
        isSetupOpen = false;
        statusMessage = null;
        runOutput = null;
        lastRunOutcome = null;
        testSummary = null;
        collectionState.Start();
        await InvokeAsync(StateHasChanged);

        var progress = new Progress<CoverageCollectionPhase>(phase =>
        {
            if (!disposed && collectionState.Advance(phase))
                StateHasChanged();
        });

        try
        {
            var result = await ViewService.RunAsync(solutionPath, LaunchContext.InvocationDirectory, settings, runCancellation.Token, progress);
            if (disposed) return;
            statusMessage = result.Message;
            statusIsError = result.Outcome != CoverageRunOutcome.Succeeded;
            runOutput = result.Output;
            lastRunOutcome = result.Outcome;
            testSummary = result.Tests;
            currentRun = result.Run;
            if (result.ReportPath is null) report = null;
            if (result.ReportPath is not null)
            {
                reportPath = result.ReportPath;
                sourceRoot = Path.GetDirectoryName(Path.GetFullPath(solutionPath)) ?? string.Empty;
                collectionState.Advance(CoverageCollectionPhase.BuildingMetrics);
                await InvokeAsync(StateHasChanged);
                await LoadReport(result.Outcome != CoverageRunOutcome.Succeeded);
            }
            else
            {
                mode = Mode.Run;
                isSetupOpen = true;
            }
        }
        catch (OperationCanceledException) when (disposed || runCancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is CoverageOperationException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            statusMessage = ex.Message;
            statusIsError = true;
            lastRunOutcome = CoverageRunOutcome.ExecutionFailed;
            mode = Mode.Run;
            isSetupOpen = true;
        }
        finally
        {
            isBusy = false;
            collectionState.Complete(lastRunOutcome ?? CoverageRunOutcome.ExecutionFailed);
        }
    }

    private Task OpenReport()
    {
        ClearTestRunResult();
        currentRun = null;
        importedReportName = null;
        return LoadReport();
    }

    private async Task ImportReportFile(InputFileChangeEventArgs args)
    {
        try
        {
            ClearTestRunResult();
            currentRun = null;
            importedReportName = Path.GetFileNameWithoutExtension(args.File.Name);
            await using var input = args.File.OpenReadStream(MaxReportBytes);
            reportPath = await ViewService.ImportAsync(input, runCancellation.Token);
            if (!disposed) await LoadReport();
        }
        catch (OperationCanceledException) when (disposed || runCancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is CoverageOperationException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            statusMessage = ex.Message;
            statusIsError = true;
        }
    }

    private async Task LoadReport(bool preserveRunStatus = false)
    {
        try
        {
            var loaded = await ViewService.LoadAsync(Path.GetFullPath(reportPath, LaunchContext.InvocationDirectory),
                string.IsNullOrWhiteSpace(sourceRoot) ? null : Path.GetFullPath(sourceRoot, LaunchContext.InvocationDirectory), runCancellation.Token);
            report = loaded.Report;
            currentRun ??= loaded.Run;
            if (currentRun is not null)
            {
                report = report with
                {
                    Name = CoverageRunLabelFormatter.Format(currentRun),
                    GeneratedAt = currentRun.StartedAt
                };
            }
            else if (!string.IsNullOrWhiteSpace(importedReportName))
            {
                report = report with { Name = importedReportName };
            }
            workspaceMode = WorkspaceMode.Metrics;
            metricSelection = null;
            failureSelection = null;
            if (!preserveRunStatus)
            {
                statusMessage = $"Loaded {report.Packages.Count} projects and {report.Files.Count} files.";
                statusIsError = false;
            }
            isSetupOpen = preserveRunStatus;
        }
        catch (OperationCanceledException) when (disposed || runCancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is CoverageOperationException or ArgumentException or IOException or UnauthorizedAccessException
            or System.Xml.XmlException or NotSupportedException)
        {
            report = null;
            statusMessage = ex.Message;
            statusIsError = true;
            isSetupOpen = true;
        }
    }

    private void ClearTestRunResult()
    {
        lastRunOutcome = null;
        testSummary = null;
        runOutput = null;
    }

    private string? RunDetailsTitle => currentRun is null
        ? null
        : $"Run {currentRun.Id} · {currentRun.StartedAt:O}";

    private string RunStatusClass => currentRun?.Status switch
    {
        CoverageRunStatus.Succeeded => "completed",
        CoverageRunStatus.TestsFailed => "test-failures",
        CoverageRunStatus.Created or CoverageRunStatus.Running or CoverageRunStatus.CancellationRequested => "incomplete",
        CoverageRunStatus.Cancelled => "cancelled",
        _ => "failed"
    };

    private static string FailureHeading(int count) => count == 1 ? "1 test failed" : $"{count} tests failed";





    private bool CanOpenFailureSource(TestFailure failure) => FindFailureFile(failure) is not null;

    private Task OpenFailureSource(TestFailure failure)
    {
        workspaceMode = WorkspaceMode.Explorer;
        isSetupOpen = false;
        failureSelection = failure;
        metricSelection = null;
        return Task.CompletedTask;
    }

    private (PackageCoverage Project, FileCoverage File)? FindFailureFile(TestFailure failure)
    {
        if (report is null || string.IsNullOrWhiteSpace(failure.SourceFile)) return null;
        try
        {
            var failurePath = Path.GetFullPath(failure.SourceFile);
            foreach (var project in report.Packages)
            {
                var file = project.Files.FirstOrDefault(x => x.ResolvedPath is not null
                    && string.Equals(Path.GetFullPath(x.ResolvedPath), failurePath, StringComparison.OrdinalIgnoreCase));
                if (file is not null) return (project, file);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { }
        return null;
    }

    private async Task CopyFailure(TestFailure failure)
    {
        try { await ViewService.CopyFailureAsync(failure); }
        catch (CoverageOperationException exception)
        {
            statusMessage = exception.Message;
            statusIsError = true;
            isSetupOpen = true;
        }
    }

    private Task OpenMetricClass(CoverageMetricRow row)
    {
        workspaceMode = WorkspaceMode.Explorer;
        isSetupOpen = false;
        metricSelection = row;
        failureSelection = null;
        return Task.CompletedTask;
    }
    private void ApplySolutionSelection(string path)
    {
        solutionPath = path;
        ClearTestRunResult();
        statusMessage = $"Selected {Path.GetFileName(path)}.";
        statusIsError = false;
    }

}
