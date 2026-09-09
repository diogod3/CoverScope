using System.Xml.Linq;
using DD3.CoverScope.Brokers;
using DD3.CoverScope.Models.Reviews;

namespace DD3.CoverScope.Services.Reviews;

public sealed class VerificationService(IProcessBroker processes, IFileSystemBroker files, ReportReader reports)
{
    public async Task TestsAsync(ReviewEvidence evidence, string directory, Action progress, CancellationToken token, Func<CancellationToken, Task>? validateSource = null)
    {
        var target = evidence.Comparison.TargetPath;
        var root = evidence.Comparison.Repository;
        var workingDirectory = Path.GetDirectoryName(target)!;
        var settings = evidence.Settings;
        var output = Path.Combine(directory, "tests");
        files.CreateDirectory(output);
        evidence.Build.Status = CheckStatus.Running;
        progress();
        try
        {
            var version = await processes.ExecuteAsync(new("dotnet", ["--version"], Path.GetDirectoryName(target)!), token);
            evidence.Build.ToolVersion = evidence.Tests.ToolVersion = version.Output.Trim();
            var global = FindGlobalJson(workingDirectory);
            if (global is not null && (await files.ReadAsync(global, token)).Contains("Microsoft.Testing.Platform", StringComparison.Ordinal))
            { throw new NotSupportedException("This target selects Microsoft.Testing.Platform. Initial collection supports VSTest; Git and formatting remain available."); }
            var build = await RunAsync(evidence.Build, workingDirectory, ["build", target, "--configuration", settings.Configuration, "--disable-build-servers", "-p:UseSharedCompilation=false"], token);
            if (build.ExitCode != 0)
            {
                evidence.Build.Status = CheckStatus.FailedToExecute;
                evidence.Build.Message = "Build failed; tests and coverage were blocked.";
                evidence.Tests.Status = CheckStatus.Blocked;
                return;
            }
            evidence.Build.Status = CheckStatus.Completed;
            evidence.Build.Message = "Build succeeded in this run.";
            if (validateSource is not null) { await validateSource(token); }
            evidence.Tests.Status = CheckStatus.Running;
            progress();
            var runsettings = Path.Combine(output, "coverage.runsettings");
            await files.WriteAsync(runsettings, new XDocument(new XElement("RunSettings", new XElement("DataCollectionRunSettings",
                new XElement("DataCollectors", new XElement("DataCollector", new XAttribute("friendlyName", "XPlat Code Coverage"),
                    new XElement("Configuration", new XElement("Format", "json,cobertura"))))))).ToString(), token);
            var result = await RunAsync(evidence.Tests, workingDirectory, ["test", target, "--configuration", settings.Configuration, "--no-build", "--no-restore",
                "--collect:XPlat Code Coverage", "--results-directory", output, "--logger", "trx;LogFilePrefix=coverscope", "--settings", runsettings], token);
            foreach (var path in files.Files(output, "*.trx", SearchOption.AllDirectories))
            { evidence.Executions.AddRange(reports.ReadTests(await files.ReadAsync(path, token), Path.GetRelativePath(output, path))); }
            evidence.Tests.Status = evidence.Executions.Count > 0 ? CheckStatus.Completed : CheckStatus.FailedToExecute;
            var failed = evidence.Executions.Count(x => x.Outcome is "Failed" or "Error" or "Timeout" or "Aborted");
            evidence.Tests.Message = evidence.Executions.Count == 0 ? "No individual test results were produced. See tool output."
                : result.ExitCode != 0 && failed == 0 ? "Runner returned an error; execution totals may be incomplete." : $"{evidence.Executions.Count} executions; {failed} failed/error/aborted outcomes.";
            if (result.ExitCode != 0 && failed == 0) { evidence.Tests.Status = CheckStatus.FailedToExecute; }
            try
            {
                var artifacts = new List<(string Name, string Json)>();
                foreach (var path in files.Files(output, "coverage.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
                { artifacts.Add((Path.GetRelativePath(output, path), await files.ReadAsync(path, token))); }
                evidence.Coverage = reports.ReadCoverageArtifacts(artifacts, root);
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not ProcessStoppingException)
            { evidence.Coverage = new() { Limitation = "Coverage report could not be read: " + ex.Message }; }

        }
        catch (Exception ex) when (ex is not OperationCanceledException and not ProcessStoppingException)
        {
            var check = evidence.Build.Status == CheckStatus.Running ? evidence.Build : evidence.Tests;
            check.Status = CheckStatus.FailedToExecute;
            check.Message = ex.Message;
            if (check == evidence.Build) { evidence.Tests.Status = CheckStatus.Blocked; }
        }
        finally { progress(); }
    }

    public async Task FormattingAsync(ReviewEvidence evidence, string directory, Action progress, CancellationToken token)
    {
        var check = evidence.Formatting;
        check.Status = CheckStatus.Running;
        progress();
        try
        {
            var root = evidence.Comparison.Repository;
            var target = evidence.Comparison.TargetPath;
            var version = await processes.ExecuteAsync(new("dotnet", ["format", "--version"], Path.GetDirectoryName(target)!), token);
            check.ToolVersion = version.Output.Trim();
            RecordConfigurationCandidates(evidence);
            foreach (var kind in new[] { "whitespace", "style" })
            {
                var reportDirectory = Path.Combine(directory, "format-" + kind);
                files.CreateDirectory(reportDirectory);
                var arguments = new List<string> { "format", kind, target, "--verify-no-changes", "--report", reportDirectory };
                if (kind == "style") { arguments.AddRange(["--severity", "info"]); }
                var result = await RunAsync(check, Path.GetDirectoryName(target)!, arguments.ToArray(), token);
                var paths = files.Files(reportDirectory, "*.json");
                var before = evidence.FormattingFindings.Count;
                foreach (var path in paths) { evidence.FormattingFindings.AddRange(reports.ReadFormatting(await files.ReadAsync(path, token), root)); }
                RecordConfigurationCandidates(evidence);
                if (result.ExitCode != 0 && evidence.FormattingFindings.Count == before)
                { throw new InvalidOperationException($"Formatting {kind} could not complete. See tool output."); }
                if (result.ExitCode == 0 && paths.Length == 0)
                { throw new InvalidDataException($"Formatting {kind} produced no structured report; findings are unavailable."); }
            }
            check.Status = CheckStatus.Completed;
            check.Message = $"{evidence.FormattingFindings.Count} findings. Analysis only; dotnet format uses its default project configuration and repository rules.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not ProcessStoppingException)
        { check.Status = CheckStatus.FailedToExecute; check.Message = ex.Message; }
        finally { progress(); }
    }

    private string? FindGlobalJson(string workingDirectory)
    {
        for (var parent = new DirectoryInfo(workingDirectory); parent is not null; parent = parent.Parent)
        {
            var path = Path.Combine(parent.FullName, "global.json");
            if (files.FileExists(path)) { return path; }
        }
        return null;
    }

    private void RecordConfigurationCandidates(ReviewEvidence evidence)
    {
        // Only ancestor candidates for the target and reported source files, not every config
        // inside restored packages. Discovery does not establish effective rule precedence.
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var configs = evidence.FormattingConfiguration.ToHashSet(comparer);
        var visited = new HashSet<string>(comparer);
        var paths = evidence.FormattingFindings.Select(x => Path.GetFullPath(x.Path, evidence.Comparison.Repository))
            .Append(evidence.Comparison.TargetPath);
        foreach (var path in paths)
        {
            for (var parent = new DirectoryInfo(Path.GetDirectoryName(path)!); parent is not null; parent = parent.Parent)
            {
                if (!visited.Add(parent.FullName)) { break; }
                var config = Path.Combine(parent.FullName, ".editorconfig");
                if (files.FileExists(config)) { configs.Add(config.Replace('\\', '/')); }
            }
        }
        evidence.FormattingConfiguration = configs.Order(comparer).ToList();
    }

    private async Task<ProcessResult> RunAsync(CheckEvidence check, string root, string[] arguments, CancellationToken token)
    {
        check.StartedAt ??= DateTimeOffset.UtcNow;
        check.Commands.Add("dotnet " + string.Join(" ", arguments.Select(x => x.Contains(' ') ? "\"" + x + "\"" : x)));
        var result = await processes.ExecuteAsync(new("dotnet", arguments, root), token);
        check.ExitCode = result.ExitCode;
        check.Output += result.Output + "\n" + result.Error;
        check.EndedAt = DateTimeOffset.UtcNow;
        return result;
    }
}
