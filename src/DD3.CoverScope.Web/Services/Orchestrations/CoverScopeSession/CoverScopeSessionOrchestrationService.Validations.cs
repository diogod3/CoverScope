using DD3.CoverScope.Models;
namespace DD3.CoverScope.Services.Orchestrations.CoverScopeSession;
public partial class CoverScopeSessionOrchestrationService
{
    private static void ValidateRequest(CoverScopeSessionRequest request) => ArgumentNullException.ThrowIfNull(request);
}
