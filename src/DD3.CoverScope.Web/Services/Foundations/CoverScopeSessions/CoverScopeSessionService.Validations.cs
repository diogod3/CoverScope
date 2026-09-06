using DD3.CoverScope.Models;
namespace DD3.CoverScope.Services.Foundations.CoverScopeSessions;
public partial class CoverScopeSessionService
{
    private static void ValidateRequest(CoverScopeSessionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Path.IsPathFullyQualified(request.InvocationDirectory) || request.Port is < 1 or > 65535)
            throw new ArgumentException("A session requires an absolute invocation directory and an available valid port.");
    }
}
