using DD3.CoverScope.Models.Exceptions;
using Xunit;
namespace DD3.CoverScope.Tests;
public partial class CoverageReportServiceTests
{
    [Fact]
    public void ValidateReport_AcceptsCoberturaWithoutResolvingSources()
    {
        var path = WriteReport("<coverage><packages /></coverage>");
        TestServices.CreateCoverageReportService().ValidateReport(path);
    }
}
