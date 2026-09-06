using DD3.CoverScope.Models;
namespace DD3.CoverScope.Services.Foundations.TestRunSummaries;
public interface ITestRunSummaryService
{
    TestRunSummary Parse(IEnumerable<string> reportPaths);
}
