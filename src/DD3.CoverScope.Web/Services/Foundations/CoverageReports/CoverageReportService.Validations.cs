using DD3.CoverScope.Models;
namespace DD3.CoverScope.Services.Foundations.CoverageReports;
public partial class CoverageReportService
{
    private static void ValidateParse(string reportPath, string? sourceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
    }
    private static void ValidateReadSource(FileCoverage file)
    {
        ArgumentNullException.ThrowIfNull(file);
    }
}
