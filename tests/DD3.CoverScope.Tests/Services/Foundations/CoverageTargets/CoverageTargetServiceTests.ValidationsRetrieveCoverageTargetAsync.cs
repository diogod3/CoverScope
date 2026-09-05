using DD3.CoverScope.Models.CoverageTargets.Exceptions;
using Xunit;

namespace DD3.CoverScope.Tests.Services.Foundations.CoverageTargets;

public partial class CoverageTargetServiceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Demo.txt")]
    [InlineData("Demo.sln.bak")]
    [InlineData("invalid\0.sln")]
    public async Task ShouldRejectInvalidTargetBeforeFilesystemAccessAsync(string? path)
    {
        var exception = await Assert.ThrowsAsync<CoverageTargetValidationException>(
            () => service.RetrieveCoverageTargetAsync(path!, baseDirectory).AsTask());

        Assert.IsType<InvalidCoverageTargetException>(exception.InnerException);
        Assert.Empty(fileSystemBroker.RequestedPaths);
        Assert.Equal(new[] { "trace", "failed" }, events);
        Assert.Same(exception, diagnosticsBroker.ObservedException);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("relative-directory")]
    public async Task ShouldRejectInvalidBaseDirectoryAsync(string? directory)
    {
        var exception = await Assert.ThrowsAsync<CoverageTargetValidationException>(
            () => service.RetrieveCoverageTargetAsync("Demo.sln", directory!).AsTask());

        Assert.IsType<InvalidCoverageTargetException>(exception.InnerException);
        Assert.Empty(fileSystemBroker.RequestedPaths);
    }

    [Fact]
    public async Task ShouldRejectMissingTargetWithResolvedPathAsync()
    {
        fileSystemBroker.Exists = false;
        string expectedPath = Path.Combine(baseDirectory, "Missing.sln");

        var exception = await Assert.ThrowsAsync<CoverageTargetValidationException>(
            () => service.RetrieveCoverageTargetAsync("Missing.sln", baseDirectory).AsTask());

        Assert.IsType<NotFoundCoverageTargetException>(exception.InnerException);
        Assert.Contains(expectedPath, exception.Message);
        Assert.Equal(expectedPath, Assert.Single(fileSystemBroker.RequestedPaths));
        Assert.Equal(new[] { "trace", "filesystem", "failed" }, events);
    }
}
