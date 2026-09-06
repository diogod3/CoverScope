using CoverScopeSession = DD3.CoverScope.Models.CoverScopeSession;
using DD3.CoverScope.Services.Foundations.CoverScopeSessions;
using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Models;
using DD3.CoverScope.Services.Foundations.CoverageTargets;

namespace DD3.CoverScope.Services.Orchestrations.CoverScopeSession;

public interface ICoverScopeSessionOrchestrationService
{
    ValueTask RunAsync(CoverScopeSessionRequest request, Func<CoverScopeSession, ValueTask> onStarted,
        CancellationToken cancellationToken = default);
}
public partial class CoverScopeSessionOrchestrationService(ICoverageTargetService targetService,
    ICoverScopeSessionService sessionService, IDiagnosticsBroker diagnosticsBroker) : ICoverScopeSessionOrchestrationService
{
    public ValueTask RunAsync(CoverScopeSessionRequest request, Func<CoverScopeSession, ValueTask> onStarted,
        CancellationToken cancellationToken = default) => Trace(() => TryCatch(async () =>
    {
        ValidateRequest(request);
        if (request.TargetPath is not null)
        {
            var target = await targetService.RetrieveCoverageTargetAsync(request.TargetPath, request.InvocationDirectory, cancellationToken);
            request = request with { TargetPath = target.Path };
        }
        await sessionService.RunAsync(request, onStarted, cancellationToken);
    }));
}
