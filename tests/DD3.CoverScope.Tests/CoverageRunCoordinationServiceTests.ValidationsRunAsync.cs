using DD3.CoverScope.Models;
using Xunit;

namespace DD3.CoverScope.Tests;

public partial class CoverageRunCoordinationServiceTests
{
    [Theory]
    [InlineData("")]
    [InlineData("Missing.sln")]
    [InlineData("README.md")]
    [InlineData("invalid\0.sln")]
    public async Task ShouldRejectInvalidTargetWithoutAllocatingRunAsync(string path)
    {
        var runner = CreateRunner();

        var result = await runner.RunAsync(path, directory, new CoverageSettings());

        Assert.Equal(CoverageRunOutcome.ExecutionFailed, result.Outcome);
        Assert.Null(result.Run);
        Assert.Null(result.ReportPath);
        Assert.False(Directory.Exists(directory));
    }
}
