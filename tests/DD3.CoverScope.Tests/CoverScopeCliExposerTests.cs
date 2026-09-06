using DD3.CoverScope.Services.Orchestrations.CoverScopeSession;
using DD3.CoverScope.Brokers.BrowserLaunchers;
using DD3.CoverScope.Brokers.Consoles;
using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Exposers.Cli;
using DD3.CoverScope.Models;
using DD3.CoverScope.Services;

namespace DD3.CoverScope.Tests;
public partial class CoverScopeCliExposerTests
{
    private readonly ConsoleFake console = new();
    private readonly BrowserFake browser = new();
    private readonly SessionFake sessions = new();
    private CoverScopeCliExposer CreateExposer() => new(console, browser, sessions, new DiagnosticsBroker());
    private class ConsoleFake : IConsoleBroker
    {
        public List<string> Output { get; } = [];
        public List<string> Errors { get; } = [];
        public string GetInvocationDirectory() => Path.GetTempPath();
        public void WriteLine(string text) => Output.Add(text);
        public void WriteError(string text) => Errors.Add(text);
    }
    private class BrowserFake : IBrowserLauncherBroker
    {
        public int Calls { get; private set; }
        public bool Fail { get; set; }
        public void Open(Uri uri)
        {
            Calls++;
            if (Fail) throw new InvalidOperationException("No browser configured.");
        }
    }
    private class SessionFake : ICoverScopeSessionOrchestrationService
    {
        public int Calls { get; private set; }
        public Exception? Failure { get; set; }
        public async ValueTask RunAsync(CoverScopeSessionRequest request, Func<CoverScopeSession, ValueTask> onStarted,
            CancellationToken cancellationToken)
        {
            Calls++;
            if (Failure is not null) throw Failure;
            var timestamp = DateTimeOffset.UtcNow;
            await onStarted(new(Guid.CreateVersion7(timestamp), timestamp, request.InvocationDirectory, request.TargetPath,
                new("http://coverscope.localhost:43123"), new("http://127.0.0.1:43123")));
        }
    }
}
