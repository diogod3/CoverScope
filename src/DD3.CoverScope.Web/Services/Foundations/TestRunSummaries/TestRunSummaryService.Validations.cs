using DD3.CoverScope.Models;
namespace DD3.CoverScope.Services.Foundations.TestRunSummaries;
public partial class TestRunSummaryService
{
    private static void ValidateParse(IEnumerable<string> reportPaths)
    {
        ArgumentNullException.ThrowIfNull(reportPaths);
    }
}
