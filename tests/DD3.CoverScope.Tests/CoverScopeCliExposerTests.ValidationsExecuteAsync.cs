using Xunit;
namespace DD3.CoverScope.Tests;
public partial class CoverScopeCliExposerTests
{
    [Fact]
    public async Task ExecuteAsync_RejectsBadPortBeforeStartingSession()
    {
        Assert.Equal(2, await CreateExposer().ExecuteAsync(["--port", "0"]));
        Assert.Equal(0, sessions.Calls);
        Assert.NotEmpty(console.Errors);
    }
}
