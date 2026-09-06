using DD3.CoverScope.Models;
using DD3.CoverScope.Services.Orchestrations.CoverScopeSession;
using DD3.CoverScope.Brokers.BrowserLaunchers;
using DD3.CoverScope.Brokers.Consoles;
using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Models.Exceptions;
using DD3.CoverScope.Services;

namespace DD3.CoverScope.Exposers.Cli;

public partial class CoverScopeCliExposer(IConsoleBroker consoleBroker, IBrowserLauncherBroker browserLauncherBroker,
    ICoverScopeSessionOrchestrationService sessionService, IDiagnosticsBroker diagnosticsBroker)
{
    public Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default) =>
        Trace(() => TryCatch(async () =>
    {
        var parsed = CoverScopeCommandLine.Parse(args, consoleBroker.GetInvocationDirectory());
        if (!parsed.Success)
        {
            consoleBroker.WriteError($"coverscope: {parsed.ErrorMessage}");
            consoleBroker.WriteError("Run 'coverscope --help' for usage.");
            return 2;
        }
        var options = parsed.Options!;
        if (options.ShowHelp) { consoleBroker.WriteLine(CoverScopeCommandLine.HelpText); return 0; }
        if (options.ShowVersion) { consoleBroker.WriteLine(CoverScopeCommandLine.GetVersion()); return 0; }

        await sessionService.RunAsync(new(options.InvocationDirectory, options.TargetPath, options.Port), session =>
        {
            consoleBroker.WriteLine($"CoverScope is running at {session.PreferredUrl.GetLeftPart(UriPartial.Authority)}");
            consoleBroker.WriteLine($"Loopback fallback: {session.FallbackUrl.GetLeftPart(UriPartial.Authority)}");
            consoleBroker.WriteLine("Press Ctrl+C to stop.");
            if (options.OpenBrowser)
            {
                try { browserLauncherBroker.Open(session.PreferredUrl); }
                catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
                { consoleBroker.WriteError($"CoverScope could not open the browser automatically: {exception.Message}"); }
            }
            return ValueTask.CompletedTask;
        }, cancellationToken);
        return 0;
    })).AsTask();
}
