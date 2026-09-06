using System.Diagnostics;

namespace DD3.CoverScope.Brokers.Processes;

public record ProcessResult(int ExitCode, string Output);
public class ProcessCancelledException(ProcessResult result, CancellationToken cancellationToken)
    : OperationCanceledException("The process was cancelled.", cancellationToken)
{
    public ProcessResult Result { get; } = result;
}

public interface IProcessBroker
{
    ValueTask<ProcessResult> ExecuteAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken = default);
}

public class ProcessBroker : IProcessBroker
{
    public async ValueTask<ProcessResult> ExecuteAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Kill the whole dotnet test tree before finalizing the run, otherwise children
            // can continue writing reports after a Cancelled manifest has been persisted.
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                await process.WaitForExitAsync(cleanup.Token);
                await Task.WhenAll(stdout, stderr).WaitAsync(cleanup.Token);
            }
            catch (OperationCanceledException) { }
            var output = string.Join(Environment.NewLine,
                stdout.IsCompletedSuccessfully ? stdout.Result : string.Empty,
                stderr.IsCompletedSuccessfully ? stderr.Result : string.Empty).Trim();
            throw new ProcessCancelledException(new(process.HasExited ? process.ExitCode : -1, output), cancellationToken);
        }

        return new(process.ExitCode, string.Join(Environment.NewLine, await stdout, await stderr).Trim());
    }
}
