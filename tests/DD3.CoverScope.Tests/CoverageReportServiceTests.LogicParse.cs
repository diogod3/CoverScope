using DD3.CoverScope.Services;
using Xunit;

namespace DD3.CoverScope.Tests;

public partial class CoverageReportServiceTests
{
    [Fact]
    public void Parse_ComputesLineAndBranchMetricsAndResolvesSource()
    {
        File.WriteAllLines(Path.Combine(directory, "Calculator.cs"), ["line one", "line two", "line three"]);
        var reportPath = WriteReport("""
            <?xml version="1.0"?>
            <coverage timestamp="1700000000">
              <sources><source>SOURCE_ROOT</source></sources>
              <packages><package name="Demo.Core"><classes>
                <class name="Demo.Math.Calculator" filename="Calculator.cs"><methods>
                  <method name="Add" signature="()"><lines><line number="1" hits="2" /></lines></method>
                </methods><lines>
                  <line number="1" hits="2" />
                  <line number="2" hits="0" branch="true" condition-coverage="50% (1/2)" />
                  <line number="3" hits="1" />
                </lines></class>
              </classes></package></packages>
            </coverage>
            """.Replace("SOURCE_ROOT", directory));

        var result = TestServices.CreateCoverageReportService().Parse(reportPath);

        Assert.Equal(2, result.Lines.Covered);
        Assert.Equal(3, result.Lines.Total);
        Assert.Equal(1, result.Branches.Covered);
        Assert.Equal(2, result.Branches.Total);
        Assert.Equal("Demo.Core", result.Packages.Single().Name);
        Assert.Equal("Demo.Math", result.Packages.Single().Namespaces.Single().Name);
        Assert.Equal("Calculator", result.Packages.Single().Namespaces.Single().Classes.Single().Name);
        Assert.Equal("Add", result.Packages.Single().Namespaces.Single().Classes.Single().Methods.Single().Name);
        Assert.Equal(Path.Combine(directory, "Calculator.cs"), result.Files.Single().ResolvedPath);
    }

    [Fact]
    public void Parse_MergesClassesThatShareAFileWithoutDoubleCountingLines()
    {
        var reportPath = WriteReport("""
            <coverage><packages><package name="Demo"><classes>
              <class name="One" filename="Shared.cs"><lines><line number="4" hits="0" /></lines></class>
              <class name="Two" filename="Shared.cs"><lines><line number="4" hits="3" /><line number="5" hits="0" /></lines></class>
            </classes></package></packages></coverage>
            """);

        var file = TestServices.CreateCoverageReportService().Parse(reportPath).Files.Single();

        Assert.Equal(2, file.Lines.Count);
        Assert.Equal(1, file.LineMetric.Covered);
        Assert.Contains(file.Classes, x => x.Name == "One");
        Assert.Contains(file.Classes, x => x.Name == "Two");
    }

    [Fact]
    public void Parse_DeduplicatesTheSameClassAndMethodAcrossMergedReports()
    {
        var reportPath = WriteReport("""
            <coverage><packages>
              <package name="Demo"><classes>
                <class name="Demo.Worker" filename="Worker.cs"><methods>
                  <method name="Run" signature="()"><lines><line number="10" hits="1" /></lines></method>
                </methods><lines><line number="10" hits="1" /><line number="11" hits="0" /></lines></class>
              </classes></package>
              <package name="Demo"><classes>
                <class name="Demo.Worker" filename="Worker.cs"><methods>
                  <method name="Run" signature="()"><lines><line number="10" hits="0" /></lines></method>
                </methods><lines><line number="10" hits="0" /><line number="11" hits="2" /></lines></class>
              </classes></package>
            </packages></coverage>
            """);

        var project = TestServices.CreateCoverageReportService().Parse(reportPath).Packages.Single();
        var classItem = project.Namespaces.Single().Classes.Single();

        Assert.Single(project.Files);
        Assert.Single(project.Files.Single().Classes);
        Assert.Single(classItem.Methods);
        Assert.Equal(2, classItem.LineMetric.Covered);
        Assert.Equal(2, classItem.LineMetric.Total);
        Assert.Equal(1, classItem.Methods.Single().Lines.Covered);
    }

    [Fact]
    public void Parse_KeepsPartialClassFragmentsInDifferentFilesSeparate()
    {
        var reportPath = WriteReport("""
            <coverage><packages><package name="Demo"><classes>
              <class name="Demo.PartialWorker" filename="PartialWorker.Commands.cs">
                <lines><line number="8" hits="1" /></lines>
              </class>
              <class name="Demo.PartialWorker" filename="PartialWorker.Queries.cs">
                <lines><line number="12" hits="0" /></lines>
              </class>
            </classes></package></packages></coverage>
            """);

        var project = TestServices.CreateCoverageReportService().Parse(reportPath).Packages.Single();
        var fragments = project.Namespaces.Single().Classes;

        Assert.Equal(2, project.Files.Count);
        Assert.Equal(2, fragments.Count);
        Assert.All(fragments, x => Assert.Equal("PartialWorker", x.Name));
        Assert.Equal(2, fragments.Select(x => x.RelativePath).Distinct().Count());
    }
}
