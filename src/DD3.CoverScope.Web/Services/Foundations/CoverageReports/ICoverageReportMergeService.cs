using DD3.CoverScope.Models;
namespace DD3.CoverScope.Services.Foundations.CoverageReports;
public interface ICoverageReportMergeService
{
    string Merge(IReadOnlyList<string> reportPaths, string destinationPath);
}
