using DD3.CoverScope.Models.Exceptions;
using Xunit;
namespace DD3.CoverScope.Tests;
public partial class CoverScopeCliExposerTests
{
    [Fact]
    public async Task ExecuteAsync_TreatsBrowserFailureAsNonFatal()
    {
        browser.Fail = true;
        Assert.Equal(0, await CreateExposer().ExecuteAsync([]));
        Assert.Contains(console.Errors, line => line.Contains("could not open the browser"));
    }
    [Fact]
    public async Task ExecuteAsync_MapsTargetValidationToUsageExitCode()
    {
        sessions.Failure = new CoverageOperationDependencyValidationException("Session", new ArgumentException("Invalid target."));
        Assert.Equal(2, await CreateExposer().ExecuteAsync(["missing.sln"]));
        Assert.Equal(0, browser.Calls);
    }
}
