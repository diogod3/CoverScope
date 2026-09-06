using DD3.CoverScope.Models;
using DD3.CoverScope.Services;
using Xunit;

namespace DD3.CoverScope.Tests;

public partial class TestRunSummaryServiceTests
{
    [Fact]
    public void Parse_ReturnsCountsAndStructuredFailureDetails()
    {
        var sourcePath = Path.Combine(directory, "DependencyInjectionTests.cs");
        var reportPath = WriteReport("results.trx", $$"""
            <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
              <Results>
                <UnitTestResult testId="failed-id" testName="RegistersAllServices" outcome="Failed" duration="00:00:00.6400000">
                  <Output>
                    <ErrorInfo>
                      <Message>Expected service registration to succeed.</Message>
                      <StackTrace>at Demo.DependencyInjectionTests.RegistersAllServices() in {{sourcePath}}:line 28</StackTrace>
                    </ErrorInfo>
                    <StdOut>diagnostic output</StdOut>
                  </Output>
                </UnitTestResult>
                <UnitTestResult testId="passed-id" testName="ResolvesVault" outcome="Passed" duration="00:00:00.1100000" />
                <UnitTestResult testId="skipped-id" testName="RequiresDocker" outcome="NotExecuted" />
              </Results>
              <TestDefinitions>
                <UnitTest id="failed-id" name="RegistersAllServices">
                  <TestMethod codeBase="Demo.Tests.dll" className="Demo.DependencyInjectionTests" name="RegistersAllServices" />
                </UnitTest>
              </TestDefinitions>
            </TestRun>
            """);

        var summary = TestServices.CreateTestRunSummaryService().Parse([reportPath]);

        Assert.Equal(1, summary.Passed);
        Assert.Equal(1, summary.Failed);
        Assert.Equal(1, summary.Skipped);
        Assert.Equal(3, summary.Total);
        Assert.Equal(TimeSpan.FromMilliseconds(750), summary.Duration);
        var failure = Assert.Single(summary.Failures);
        Assert.Equal("RegistersAllServices", failure.Name);
        Assert.Equal("Demo.DependencyInjectionTests.RegistersAllServices", failure.FullyQualifiedName);
        Assert.Equal("Demo.Tests.dll", failure.Assembly);
        Assert.Equal("Expected service registration to succeed.", failure.Message);
        Assert.Equal("diagnostic output", failure.StandardOutput);
        Assert.Equal(sourcePath, failure.SourceFile);
        Assert.Equal(28, failure.SourceLine);
    }
}
