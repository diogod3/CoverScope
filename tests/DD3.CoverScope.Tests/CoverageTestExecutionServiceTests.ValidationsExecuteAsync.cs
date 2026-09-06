using DD3.CoverScope.Models.Exceptions;
using Xunit;
namespace DD3.CoverScope.Tests;
public partial class CoverageTestExecutionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_RejectsUnresolvedPathsBeforeStartingProcess()
    {
        await Assert.ThrowsAsync<CoverageOperationValidationException>(() =>
            CreateService().ExecuteAsync("relative.csproj", Absolute("results"), Absolute("settings")).AsTask());
        Assert.Null(broker.StartInfo);
    }
}
