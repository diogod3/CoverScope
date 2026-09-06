using DD3.CoverScope.Models;
using DD3.CoverScope.Services;
using Xunit;

namespace DD3.CoverScope.Tests;

public partial class CoverageMetricsBuilderTests
{
    [Fact]
    public void BuildNamespaces_DoesNotDoubleCountAFileLineSharedByMultipleClasses()
    {
        var report = Parse("""
            <coverage><packages><package name="Demo"><classes>
              <class name="Demo.First" filename="Shared.cs"><lines><line number="4" hits="0" /></lines></class>
              <class name="Demo.Second" filename="Shared.cs"><lines><line number="4" hits="3" /><line number="5" hits="0" /></lines></class>
            </classes></package></packages></coverage>
            """);
        var project = report.Packages.Single();

        var row = new CoverageMetricsBuilder().BuildNamespaces(project).Single();

        Assert.Equal(1, row.Lines.Covered);
        Assert.Equal(2, row.Lines.Total);
        Assert.Equal(1, row.FileCount);
        Assert.Equal(2, row.ChildCount);
    }
}
