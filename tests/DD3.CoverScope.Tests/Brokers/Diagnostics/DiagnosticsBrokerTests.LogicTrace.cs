using System.Diagnostics;
using Xunit;

namespace DD3.CoverScope.Tests.Brokers.Diagnostics;

public partial class DiagnosticsBrokerTests
{
    [Fact]
    public async Task ShouldExecuteOperationAndReturnItsResultAsync()
    {
        int calls = 0;
        Guid expected = Guid.NewGuid();

        var result = await broker.Trace(() =>
        {
            calls++;
            return ValueTask.FromResult(expected);
        }, $"test-{Guid.NewGuid():N}");

        Assert.Equal(expected, result);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ShouldStopActivityAndRestoreParentAsync()
    {
        string operationName = $"test-{Guid.NewGuid():N}";
        var stopped = new List<Activity>();
        using var listener = Listen(operationName, stopped);
        ActivitySource.AddActivityListener(listener);
        using var parent = new Activity("test-parent").Start();
        string? observedParentId = null;

        await broker.Trace(() =>
        {
            observedParentId = Activity.Current?.ParentId;
            return ValueTask.FromResult(true);
        }, operationName);

        Assert.Equal(parent.Id, observedParentId);
        Assert.Same(parent, Activity.Current);
        Assert.Equal(operationName, Assert.Single(stopped).OperationName);
    }
}
