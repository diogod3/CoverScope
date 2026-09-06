using DD3.CoverScope.Models;
namespace DD3.CoverScope.Services.Orchestrations.CoverageCollection;
public partial class CoverageCollectionOrchestrationService
{
    private static void ValidateRun(CoverageRunContext run)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (!Path.IsPathFullyQualified(run.DirectoryPath))
            throw new ArgumentException("The run directory must be absolute.", nameof(run));
    }
}
