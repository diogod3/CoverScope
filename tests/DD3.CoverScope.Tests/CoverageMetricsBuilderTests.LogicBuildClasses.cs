using DD3.CoverScope.Models;
using DD3.CoverScope.Services;
using Xunit;

namespace DD3.CoverScope.Tests;

public partial class CoverageMetricsBuilderTests
{
    [Fact]
    public void BuildClasses_AggregatesPartialClassFragmentsWithoutMergingSameLineNumbersAcrossFiles()
    {
        var report = Parse("""
            <coverage><packages><package name="Demo"><classes>
              <class name="Demo.PartialWorker" filename="PartialWorker.Commands.cs"><methods>
                <method name="Execute" signature="()"><lines><line number="10" hits="2" /></lines></method>
              </methods><lines><line number="10" hits="2" /></lines></class>
              <class name="Demo.PartialWorker" filename="PartialWorker.Queries.cs"><methods>
                <method name="Query" signature="()"><lines><line number="10" hits="0" /></lines></method>
              </methods><lines><line number="10" hits="0" /></lines></class>
            </classes></package></packages></coverage>
            """);
        var project = report.Packages.Single();
        var namespaceItem = project.Namespaces.Single();

        var row = new CoverageMetricsBuilder().BuildClasses(project, namespaceItem).Single();

        Assert.Equal(1, row.Lines.Covered);
        Assert.Equal(2, row.Lines.Total);
        Assert.Equal(1, row.Methods.Covered);
        Assert.Equal(2, row.Methods.Total);
        Assert.Equal(2, row.FileCount);
        Assert.Equal(2, row.ClassFragments.Count);
    }
}
