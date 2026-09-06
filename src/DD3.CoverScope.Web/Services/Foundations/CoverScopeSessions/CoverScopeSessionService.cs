using CoverScopeSession = DD3.CoverScope.Models.CoverScopeSession;
using DD3.CoverScope.Brokers.ApplicationHosts;
using DD3.CoverScope.Brokers.DateTimes;
using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Brokers.Identifiers;
using DD3.CoverScope.Models;

namespace DD3.CoverScope.Services.Foundations.CoverScopeSessions;

public interface ICoverScopeSessionService
{
    ValueTask RunAsync(CoverScopeSessionRequest request, Func<CoverScopeSession, ValueTask> onStarted,
        CancellationToken cancellationToken = default);
}
public partial class CoverScopeSessionService(IApplicationHostBroker applicationHostBroker,
    IDateTimeBroker dateTimeBroker, IIdentifierBroker identifierBroker, IDiagnosticsBroker diagnosticsBroker) : ICoverScopeSessionService
{
    public ValueTask RunAsync(CoverScopeSessionRequest request, Func<CoverScopeSession, ValueTask> onStarted,
        CancellationToken cancellationToken = default) => Trace(() => TryCatch(async () =>
    {
        ValidateRequest(request);
        var startedAt = dateTimeBroker.GetCurrentDateTime().ToUniversalTime();
        var id = identifierBroker.CreateIdentifier(startedAt);
        await applicationHostBroker.RunAsync(request, async boundUrl =>
        {
            var preferred = new UriBuilder(boundUrl) { Host = "coverscope.localhost" }.Uri;
            await onStarted(new(id, startedAt, request.InvocationDirectory, request.TargetPath, preferred, boundUrl));
        }, cancellationToken);
    }));
}
