using DD3.CoverScope.Models.CoverageTargets.Exceptions;
using Xunit;

namespace DD3.CoverScope.Tests.Services.Foundations.CoverageTargets;

public partial class CoverageTargetServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldBoxFilesystemFailuresAndPreserveCauseAsync(bool accessDenied)
    {
        Exception failure = accessDenied
            ? new UnauthorizedAccessException("Access denied.")
            : new IOException("Device unavailable.");
        fileSystemBroker.Failure = failure;

        var exception = await Assert.ThrowsAsync<CoverageTargetDependencyException>(
            () => service.RetrieveCoverageTargetAsync("Demo.sln", baseDirectory).AsTask());

        var localized = Assert.IsType<FailedCoverageTargetDependencyException>(exception.InnerException);
        Assert.Same(failure, localized.InnerException);
        Assert.Same(exception, diagnosticsBroker.ObservedException);
        Assert.Single(fileSystemBroker.RequestedPaths);
    }

    [Fact]
    public async Task ShouldBoxUnexpectedFailuresAndPreserveCauseAsync()
    {
        var failure = new InvalidOperationException("Unexpected failure.");
        fileSystemBroker.Failure = failure;

        var exception = await Assert.ThrowsAsync<CoverageTargetServiceException>(
            () => service.RetrieveCoverageTargetAsync("Demo.sln", baseDirectory).AsTask());

        var localized = Assert.IsType<FailedCoverageTargetServiceException>(exception.InnerException);
        Assert.Same(failure, localized.InnerException);
        Assert.Same(exception, diagnosticsBroker.ObservedException);
    }

    [Fact]
    public async Task ShouldCancelBeforeValidationOrFilesystemAccessAsync()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.RetrieveCoverageTargetAsync(
                null!, baseDirectory, cancellation.Token).AsTask());

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Empty(fileSystemBroker.RequestedPaths);
        Assert.Same(exception, diagnosticsBroker.ObservedException);
    }

    [Fact]
    public async Task ShouldNotBoxCancellationFromDependencyAsync()
    {
        var failure = new OperationCanceledException();
        fileSystemBroker.Failure = failure;

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.RetrieveCoverageTargetAsync("Demo.sln", baseDirectory).AsTask());

        Assert.Same(failure, exception);
    }
}
