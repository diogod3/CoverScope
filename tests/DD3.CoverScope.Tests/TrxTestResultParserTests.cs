using DD3.CoverScope.Services;
using Xunit;

namespace DD3.CoverScope.Tests;

public sealed class TrxTestResultParserTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"coverscope-trx-{Guid.NewGuid():N}");

    public TrxTestResultParserTests() => Directory.CreateDirectory(directory);

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

        var summary = new TrxTestResultParser().Parse([reportPath]);

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

        var summary = new TrxTestResultParser().Parse([first, second]);

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

        var summary = new TrxTestResultParser().Parse([reportPath]);

        Assert.Equal(8, summary.Passed);
        Assert.Equal(2, summary.Failed);
        Assert.Equal(2, summary.Skipped);
        Assert.Equal(12, summary.Total);
        Assert.Empty(summary.Failures);
    }

    private string WriteReport(string name, string xml)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, xml);
        return path;
    }

    public void Dispose()
    {
        try { Directory.Delete(directory, recursive: true); }
        catch (IOException) { }
    }
}
