using System.Diagnostics;
using DD3.CoverScope.Models;

namespace DD3.CoverScope.Services;

public sealed class CoverageRunner
{
    private readonly CoberturaReportMerger merger;
    private readonly CoverletRunSettingsWriter settingsWriter;
    private readonly TrxTestResultParser testResultParser;
    private readonly CoverageRunStore runStore;

    public CoverageRunner(
        CoberturaReportMerger merger,
        CoverletRunSettingsWriter settingsWriter,
        TrxTestResultParser testResultParser,
        CoverageRunStore runStore)
    {
        this.merger = merger;
        this.settingsWriter = settingsWriter;
        this.testResultParser = testResultParser;
        this.runStore = runStore;
    }

    public async Task<CoverageRunResult> RunAsync(
        string solutionPath,
        CoverageSettings settings,
        CancellationToken cancellationToken = default,
        IProgress<CoverageCollectionPhase>? progress = null)
    {
        progress?.Report(CoverageCollectionPhase.PreparingCollection);

        if (string.IsNullOrWhiteSpace(solutionPath))
            return new(CoverageRunOutcome.ExecutionFailed, "Choose a .sln, .slnx, or test project first.", string.Empty);

        var fullPath = Path.GetFullPath(solutionPath);
        if (!File.Exists(fullPath))
            return new(CoverageRunOutcome.ExecutionFailed, "The selected solution or project does not exist.", string.Empty);

        var allowedExtensions = new[] { ".sln", ".slnx", ".csproj", ".fsproj", ".vbproj" };
        if (!allowedExtensions.Contains(Path.GetExtension(fullPath), StringComparer.OrdinalIgnoreCase))
            return new(CoverageRunOutcome.ExecutionFailed, "Select a .sln, .slnx, or supported project file.", string.Empty);

        CoverageRunContext run;
        try
        {
            run = await runStore.BeginAsync(fullPath, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new(CoverageRunOutcome.ExecutionFailed, ex.Message, string.Empty);
        }

        try
        {
            var workingDirectory = CoverageRunStore.ResolveTargetRoot(fullPath);
            var resultsDirectory = run.DirectoryPath;
            var runSettingsPath = settingsWriter.Write(settings, Path.Combine(resultsDirectory, "coverscope.runsettings"));

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("test");
            startInfo.ArgumentList.Add(fullPath);
            startInfo.ArgumentList.Add("--collect:XPlat Code Coverage");
            startInfo.ArgumentList.Add("--results-directory");
            startInfo.ArgumentList.Add(resultsDirectory);
            startInfo.ArgumentList.Add("--logger");
            startInfo.ArgumentList.Add("trx;LogFilePrefix=coverscope");
            startInfo.ArgumentList.Add("--settings");
            startInfo.ArgumentList.Add(runSettingsPath);

            using var process = new Process { StartInfo = startInfo };
            progress?.Report(CoverageCollectionPhase.RunningTests);
            process.Start();
            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            progress?.Report(CoverageCollectionPhase.ProcessingReports);
            var output = string.Join(Environment.NewLine, await stdout, await stderr).Trim();

            var reports = Directory
                .EnumerateFiles(resultsDirectory, "coverage.cobertura.xml", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToArray();

            var report = reports.Length switch
            {
                0 => null,
                1 => reports[0],
                _ => merger.Merge(reports, Path.Combine(resultsDirectory, "coverage.merged.cobertura.xml"))
            };

            var testResultFiles = Directory
                .EnumerateFiles(resultsDirectory, "*.trx", SearchOption.AllDirectories)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var tests = testResultFiles.Length == 0 ? null : testResultParser.Parse(testResultFiles);

            if (tests?.Failed > 0)
            {
                var noun = tests.Failed == 1 ? "test" : "tests";
                var coverageMessage = report is null ? "No coverage report was produced." : "Coverage is still available.";
                return await CompleteAsync(
                    run,
                    CoverageRunOutcome.TestsFailed,
                    $"{tests.Failed} {noun} failed. {coverageMessage}",
                    output,
                    report,
                    tests,
                    testResultFiles);
            }

            if (process.ExitCode != 0)
                return await CompleteAsync(
                    run,
                    CoverageRunOutcome.ExecutionFailed,
                    "The test process did not complete successfully. See the full output for details.",
                    output,
                    report,
                    tests,
                    testResultFiles);
            if (report is null)
                return await CompleteAsync(
                    run,
                    CoverageRunOutcome.ExecutionFailed,
                    "Tests passed, but no Cobertura report was generated. Ensure coverlet.collector is referenced by the test projects.",
                    output,
                    null,
                    tests,
                    testResultFiles);

            var scope = reports.Length == 1 ? "1 report" : $"{reports.Length} reports";
            return await CompleteAsync(
                run,
                CoverageRunOutcome.Succeeded,
                $"Coverage collected successfully from {scope}.",
                output,
                report,
                tests,
                testResultFiles);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return await CompleteAsync(
                run,
                CoverageRunOutcome.ExecutionFailed,
                ".NET 10 SDK was not found. Install .NET 10 and ensure dotnet is on PATH.",
                ex.Message);
        }
        catch (OperationCanceledException)
        {
            return await CompleteAsync(
                run,
                CoverageRunOutcome.Cancelled,
                "The coverage run was cancelled.",
                string.Empty);
        }
        catch (Exception ex)
        {
            return await CompleteAsync(
                run,
                CoverageRunOutcome.ExecutionFailed,
                "Coverage collection failed.",
                ex.Message);
        }
    }

    private async Task<CoverageRunResult> CompleteAsync(
        CoverageRunContext run,
        CoverageRunOutcome outcome,
        string message,
        string output,
        string? reportPath = null,
        TestRunSummary? tests = null,
        IReadOnlyList<string>? testResultPaths = null)
    {
        var artifacts = new List<CoverageRunArtifact>();
        if (reportPath is not null)
        {
            artifacts.Add(new(
                "coverage",
                "cobertura",
                Path.GetRelativePath(run.DirectoryPath, reportPath)));
        }

        if (testResultPaths is not null)
        {
            artifacts.AddRange(testResultPaths.Select(path => new CoverageRunArtifact(
                "testResults",
                "trx",
                Path.GetRelativePath(run.DirectoryPath, path))));
        }

        var status = CoverageRunStore.ToStatus(outcome, reportPath is not null);
        var manifest = await runStore.CompleteAsync(run, status, artifacts, CancellationToken.None);
        return new(outcome, message, output, reportPath, tests, manifest);
    }
}
