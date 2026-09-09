using System.Diagnostics;
using System.Text.Json;
using DD3.CoverScope.Models.Reviews;

namespace DD3.CoverScope.Brokers;

public interface IProcessBroker
{
    Task<ProcessResult> ExecuteAsync(ProcessRequest request, CancellationToken token);
}

public sealed class ProcessStoppingException(string message, Exception? inner = null) : Exception(message, inner);

public sealed class ProcessBroker : IProcessBroker
{
    private const int OutputLimit = 2_000_000;

    public async Task<ProcessResult> ExecuteAsync(ProcessRequest request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = request.WorkingDirectory, RedirectStandardOutput = true,
            RedirectStandardError = true, RedirectStandardInput = true, UseShellExecute = false, CreateNoWindow = true,
            ArgumentList = { typeof(ProcessBroker).Assembly.Location, "--internal-process", JsonSerializer.Serialize(request) }
        };
        using var process = Process.Start(start) ?? throw new IOException("Could not start the run's process supervisor.");
        var error = DrainAsync(process.StandardError);
        NativeProcessGroup? group = null;
        Task<string>? output = null;
        try
        {
            var ready = await process.StandardOutput.ReadLineAsync(token).AsTask().WaitAsync(TimeSpan.FromSeconds(15), token);
            if (ready != ProcessSupervisor.Ready) { throw new IOException("Process supervisor failed to initialise: " + ready); }
            group = new NativeProcessGroup(process);
            output = DrainAsync(process.StandardOutput);
            token.ThrowIfCancellationRequested();
            await process.StandardInput.WriteLineAsync("start");
            await process.StandardInput.FlushAsync();
            await process.WaitForExitAsync(token);
            token.ThrowIfCancellationRequested();
            var exitCode = process.ExitCode;
            await StopAsync(process, group);
            return new(exitCode, await output.WaitAsync(TimeSpan.FromSeconds(10)), await error.WaitAsync(TimeSpan.FromSeconds(10)));
        }
        catch
        {
            try
            {
                await StopAsync(process, group);
                if (output is not null) { await output.WaitAsync(TimeSpan.FromSeconds(10)); }
                await error.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            { throw new ProcessStoppingException($"Could not confirm that owned process group {process.Id} stopped. Further collection is blocked until this is resolved.", ex); }
            throw;
        }
        finally { group?.Dispose(); }
    }

    private static async Task StopAsync(Process process, NativeProcessGroup? group)
    {
        if (group is not null) { group.Terminate(); }
        else if (!process.HasExited) { process.Kill(entireProcessTree: true); }
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (group is not null && !group.IsEmpty())
        {
            if (DateTime.UtcNow >= deadline) { throw new TimeoutException("Owned processes have not all stopped."); }
            await Task.Delay(25);
        }
    }

    private static async Task<string> DrainAsync(StreamReader reader)
    {
        var text = new System.Text.StringBuilder();
        var buffer = new char[4096];
        int count;
        var truncated = false;
        while ((count = await reader.ReadAsync(buffer.AsMemory())) != 0)
        {
            var keep = Math.Min(count, OutputLimit - text.Length);
            if (keep > 0) { text.Append(buffer, 0, keep); }
            truncated |= keep < count;
        }
        if (truncated) { text.AppendLine("\n[Output truncated after 2,000,000 characters]"); }
        return text.ToString();
    }
}
