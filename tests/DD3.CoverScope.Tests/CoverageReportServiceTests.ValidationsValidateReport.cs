using DD3.CoverScope.Models.Exceptions;
using Xunit;
namespace DD3.CoverScope.Tests;
public partial class CoverageReportServiceTests
{
    [Fact]
    public void ValidateReport_RejectsOtherXmlFormats()
    {
        var path = WriteReport("<TestRun />");
        Assert.Throws<CoverageOperationValidationException>(() => TestServices.CreateCoverageReportService().ValidateReport(path));
    }
}
