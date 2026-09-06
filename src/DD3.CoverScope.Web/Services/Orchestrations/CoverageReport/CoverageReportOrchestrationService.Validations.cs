using DD3.CoverScope.Models;
namespace DD3.CoverScope.Services.Orchestrations.CoverageReport;
public partial class CoverageReportOrchestrationService
{
    private static void ValidatePath(string path) => ArgumentException.ThrowIfNullOrWhiteSpace(path);
}
