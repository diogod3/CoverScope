using DD3.CoverScope.Models.Exceptions;
using Xunit;
namespace DD3.CoverScope.Tests;
public partial class CoverScopeSessionServiceTests
{
    [Fact]
    public async Task RunAsync_PreservesHostDependencyFailure()
    {
        var cause = new IOException("Port unavailable.");
        host.Failure = cause;
        var exception = await Assert.ThrowsAsync<CoverageOperationDependencyException>(() =>
            CreateService().RunAsync(new(Path.GetTempPath(), null, null), _ => ValueTask.CompletedTask).AsTask());
        Assert.Same(cause, exception.InnerException);
    }
}
