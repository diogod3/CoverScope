using DD3.CoverScope.Models.Exceptions;
using Xunit;
namespace DD3.CoverScope.Tests;
public partial class CoverScopeSessionServiceTests
{
    [Fact]
    public async Task RunAsync_RejectsInvalidPortBeforeHostCreation()
    {
        await Assert.ThrowsAsync<CoverageOperationValidationException>(() =>
            CreateService().RunAsync(new(Path.GetTempPath(), null, 0), _ => ValueTask.CompletedTask).AsTask());
        Assert.False(host.Called);
    }
}
