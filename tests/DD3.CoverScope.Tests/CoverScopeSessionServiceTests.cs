using DD3.CoverScope.Brokers.ApplicationHosts;
using DD3.CoverScope.Brokers.DateTimes;
using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Brokers.Identifiers;
using DD3.CoverScope.Models;
using DD3.CoverScope.Services.Foundations.CoverScopeSessions;
namespace DD3.CoverScope.Tests;
public partial class CoverScopeSessionServiceTests
{
    private readonly HostFake host = new();
    private static readonly DateTimeOffset StartedAt = new(2026, 9, 5, 14, 32, 15, TimeSpan.Zero);
    private CoverScopeSessionService CreateService() => new(host, new ClockFake(), new IdentifierBroker(), new DiagnosticsBroker());
    private class ClockFake : IDateTimeBroker { public DateTimeOffset GetCurrentDateTime() => StartedAt; }
    private class HostFake : IApplicationHostBroker
    {
        public string Url { get; set; } = "http://127.0.0.1:43127";
        public Exception? Failure { get; set; }
        public bool Called { get; private set; }
        public async ValueTask RunAsync(CoverScopeSessionRequest request, Func<Uri, ValueTask> onStarted, CancellationToken cancellationToken)
        {
            Called = true;
            if (Failure is not null) throw Failure;
            await onStarted(new Uri(Url));
        }
    }
}
