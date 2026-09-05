using DD3.CoverScope.Models;
using Xunit;

namespace DD3.CoverScope.Tests;

public partial class CoverageRunnerTests
{
    [Fact]
    public async Task ShouldCancelBeforeTargetRetrievalWithoutAllocatingRunAsync()
    {
        var runner = CreateRunner();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await runner.RunAsync("Demo.sln", directory, new CoverageSettings(), cancellation.Token);

        Assert.Equal(CoverageRunOutcome.Cancelled, result.Outcome);
        Assert.Null(result.Run);
        Assert.False(Directory.Exists(directory));
    }
}
