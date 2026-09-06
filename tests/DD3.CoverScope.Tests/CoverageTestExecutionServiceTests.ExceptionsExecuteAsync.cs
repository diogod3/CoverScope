using DD3.CoverScope.Models.Exceptions;
using Xunit;
namespace DD3.CoverScope.Tests;
public partial class CoverageTestExecutionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_BoxesDependencyCause()
    {
        var cause = new System.ComponentModel.Win32Exception("missing executable");
        broker.Failure = cause;
        var exception = await Assert.ThrowsAsync<CoverageOperationDependencyException>(() =>
            CreateService().ExecuteAsync(Absolute("sample.csproj"), Absolute("results"), Absolute("settings")).AsTask());
        Assert.Same(cause, exception.InnerException);
    }
    [Fact]
    public async Task ExecuteAsync_PreservesCancellation()
    {
        var cause = new OperationCanceledException();
        broker.Failure = cause;
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            CreateService().ExecuteAsync(Absolute("sample.csproj"), Absolute("results"), Absolute("settings")).AsTask());
        Assert.Same(cause, exception);
    }
}
