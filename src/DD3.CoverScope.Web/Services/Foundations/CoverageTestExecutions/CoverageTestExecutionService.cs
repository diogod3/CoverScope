using System.Diagnostics;
using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Brokers.Processes;

namespace DD3.CoverScope.Services.Foundations.CoverageTestExecutions;

public interface ICoverageTestExecutionService
{
    ValueTask<ProcessResult> ExecuteAsync(string targetPath, string resultsDirectory, string settingsPath, CancellationToken cancellationToken = default);
}

public partial class CoverageTestExecutionService(
    IProcessBroker processBroker, IDiagnosticsBroker diagnosticsBroker) : ICoverageTestExecutionService
{
    public ValueTask<ProcessResult> ExecuteAsync(string targetPath, string resultsDirectory, string settingsPath,
        CancellationToken cancellationToken = default) => Trace(() => TryCatch(async () =>
    {
        ValidatePaths(targetPath, resultsDirectory, settingsPath);
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = Path.GetDirectoryName(targetPath)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in new[] { "test", targetPath, "--collect:XPlat Code Coverage",
            "--results-directory", resultsDirectory, "--logger", "trx;LogFilePrefix=coverscope", "--settings", settingsPath })
            startInfo.ArgumentList.Add(argument);
        return await processBroker.ExecuteAsync(startInfo, cancellationToken);
    }));
}
