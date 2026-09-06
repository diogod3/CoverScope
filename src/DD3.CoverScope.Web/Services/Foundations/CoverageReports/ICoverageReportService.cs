using CoverageReport = DD3.CoverScope.Models.CoverageReport;
using DD3.CoverScope.Models;
namespace DD3.CoverScope.Services.Foundations.CoverageReports;
public interface ICoverageReportService
{
    void ValidateReport(string path);
    CoverageReport Parse(string reportPath, string? sourceRoot = null);
    IReadOnlyList<SourceLine> ReadSource(FileCoverage file);
}
