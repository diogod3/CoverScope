using DD3.CoverScope.Models;
namespace DD3.CoverScope.Services.Foundations.CoverageReports;
public partial class CoverageReportMergeService
{
    private static void ValidateMerge(IReadOnlyList<string> reportPaths, string destinationPath)
    {
        ArgumentNullException.ThrowIfNull(reportPaths); ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
    }
}
