using System.Diagnostics;
using Xunit;

namespace DD3.CoverScope.Tests.Brokers.Diagnostics;

public partial class DiagnosticsBrokerTests
{
    [Fact]
    public async Task ShouldPreserveFailureAndMarkActivityAsErrorAsync()
    {
        string operationName = $"test-{Guid.NewGuid():N}";
        var stopped = new List<Activity>();
        using var listener = Listen(operationName, stopped);
        ActivitySource.AddActivityListener(listener);
        var failure = new IOException("Private target path must not become telemetry.");

        var exception = await Assert.ThrowsAsync<IOException>(
            () => broker.Trace<bool>(() => throw failure, operationName).AsTask());

        Assert.Same(failure, exception);
        var activity = Assert.Single(stopped);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Null(activity.StatusDescription);
        Assert.Empty(activity.Tags);
    }

    [Fact]
    public async Task ShouldPreserveCancellationWithoutMarkingErrorAsync()
    {
        string operationName = $"test-{Guid.NewGuid():N}";
        var stopped = new List<Activity>();
        using var listener = Listen(operationName, stopped);
        ActivitySource.AddActivityListener(listener);
        var failure = new OperationCanceledException();

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            () => broker.Trace<bool>(() => throw failure, operationName).AsTask());

        Assert.Same(failure, exception);
        var activity = Assert.Single(stopped);
        Assert.NotEqual(ActivityStatusCode.Error, activity.Status);
        Assert.Equal(true, activity.GetTagItem("operation.cancelled"));
    }
}
