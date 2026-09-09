using System.Diagnostics;
using System.Text.Json;
using DD3.CoverScope.Models.Reviews;

namespace DD3.CoverScope.Brokers;

internal static class ProcessSupervisor
{
    internal const string Ready = "CoverScope process supervisor ready";

    public static async Task<int> RunAsync(string requestJson)
    {
        try
        {
            var request = JsonSerializer.Deserialize<ProcessRequest>(requestJson) ?? throw new ArgumentException("Missing process request.");
            NativeProcessGroup.PrepareSupervisor();
            Console.WriteLine(Ready);
            await Console.Out.FlushAsync();
            if (await Console.In.ReadLineAsync() != "start") { return 2; }
            var start = new ProcessStartInfo(request.Executable)
            {
                WorkingDirectory = request.WorkingDirectory, UseShellExecute = false,
                RedirectStandardInput = true, CreateNoWindow = true
            };
            foreach (var argument in request.Arguments) { start.ArgumentList.Add(argument); }
            start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
            start.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
            using var process = Process.Start(start) ?? throw new IOException("Could not start the requested tool.");
            process.StandardInput.Close();
            var finished = process.WaitForExitAsync();
            var disconnected = Task.Run(() => Console.In.ReadToEnd());
            if (await Task.WhenAny(finished, disconnected) == disconnected && !process.HasExited)
            {
                NativeProcessGroup.StopSupervisorGroup();
                process.Kill(entireProcessTree: true);
                return 3;
            }
            await finished;
            return process.ExitCode;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }
}
