using CoverageArtifacts = DD3.CoverScope.Models.CoverageArtifacts;
using DD3.CoverScope.Models;
namespace DD3.CoverScope.Services.Orchestrations.CoverageResults;
public partial class CoverageResultsOrchestrationService
{
    private static void ValidateArtifacts(CoverageArtifacts artifacts) => ArgumentNullException.ThrowIfNull(artifacts);
}
