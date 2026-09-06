using DD3.CoverScope.Models;
using DD3.CoverScope.Services;
using Xunit;

namespace DD3.CoverScope.Tests;

public partial class TestRunSummaryServiceTests
{
    [Fact]
    public void Parse_CombinesMultipleTestTargets()
    {
        var first = WriteReport("first.trx", """
            <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
              <Results><UnitTestResult testId="1" testName="First" outcome="Passed" duration="00:00:01" /></Results>
            </TestRun>
            """);
        var second = WriteReport("second.trx", """
            <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
              <Results>
                <UnitTestResult testId="2" testName="Second" outcome="Passed" duration="00:00:02" />
                <UnitTestResult testId="3" testName="Third" outcome="NotExecuted" />
              </Results>
            </TestRun>
            """);

        var summary = TestServices.CreateTestRunSummaryService().Parse([first, second]);

        Assert.Equal(2, summary.Passed);
        Assert.Equal(0, summary.Failed);
        Assert.Equal(1, summary.Skipped);
        Assert.Equal(3, summary.Total);
        Assert.Equal(TimeSpan.FromSeconds(3), summary.Duration);
    }

    [Fact]
    public void Parse_UsesCountersWhenIndividualResultsAreUnavailable()
    {
        var reportPath = WriteReport("counters.trx", """
            <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
              <ResultSummary outcome="Failed">
                <Counters total="12" passed="8" failed="2" notExecuted="2" />
              </ResultSummary>
            </TestRun>
            """);

        var summary = TestServices.CreateTestRunSummaryService().Parse([reportPath]);

        Assert.Equal(8, summary.Passed);
        Assert.Equal(2, summary.Failed);
        Assert.Equal(2, summary.Skipped);
        Assert.Equal(12, summary.Total);
        Assert.Empty(summary.Failures);
    }
}
