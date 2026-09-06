using DD3.CoverScope.Services;
using Xunit;

namespace DD3.CoverScope.Tests;

public partial class CoverageReportMergeServiceTests
{
    [Fact]
    public void Merge_CombinesPackagesFromAllReports()
    {
        var first = Write("first.xml", Report("One", "One.cs"));
        var second = Write("second.xml", Report("Two", "Two.cs"));
        var merged = Path.Combine(directory, "merged.xml");

        TestServices.CreateCoverageReportMergeService().Merge([first, second], merged);
        var result = TestServices.CreateCoverageReportService().Parse(merged);

        Assert.Equal(2, result.Packages.Count);
        Assert.Equal(2, result.Files.Count);
        Assert.Contains(result.Packages, x => x.Name == "One");
        Assert.Contains(result.Packages, x => x.Name == "Two");
    }
}
