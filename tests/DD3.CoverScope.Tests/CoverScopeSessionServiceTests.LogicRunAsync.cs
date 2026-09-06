using DD3.CoverScope.Models;
using Xunit;
namespace DD3.CoverScope.Tests;
public partial class CoverScopeSessionServiceTests
{
    [Theory]
    [InlineData(43127)]
    [InlineData(65535)]
    public async Task RunAsync_PreservesBoundPortAndCreatesDescriptiveSession(int port)
    {
        host.Url = $"http://127.0.0.1:{port}";
        CoverScopeSession? result = null;
        await CreateService().RunAsync(new(Path.GetTempPath(), null, null), session =>
        {
            result = session;
            return ValueTask.CompletedTask;
        });
        Assert.NotNull(result);
        Assert.Equal(7, result.Id.Version);
        Assert.Equal(StartedAt, result.StartedAt);
        Assert.Equal($"http://coverscope.localhost:{port}", result.PreferredUrl.GetLeftPart(UriPartial.Authority));
        Assert.Equal(host.Url, result.FallbackUrl.GetLeftPart(UriPartial.Authority));
        Assert.Equal(Path.GetTempPath(), result.InvocationDirectory);
    }
}
