using DD3.CoverScope.Models.Exceptions;
using Xunit;
namespace DD3.CoverScope.Tests;
public partial class CoverageReportServiceTests
{
    [Fact]
    public void ValidateReport_BoxesMissingFile()
    {
        var exception = Assert.Throws<CoverageOperationDependencyException>(() =>
            TestServices.CreateCoverageReportService().ValidateReport(Path.Combine(directory, "missing.xml")));
        Assert.IsType<FileNotFoundException>(exception.InnerException);
    }
}
