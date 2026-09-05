using System.Diagnostics;
using DD3.CoverScope.Brokers.Diagnostics;
using Xunit;

namespace DD3.CoverScope.Tests.Brokers.Diagnostics;

public partial class DiagnosticsBrokerTests
{
    private readonly DiagnosticsBroker broker = new();

    private static ActivityListener Listen(string operationName, List<Activity> stopped) => new()
    {
        ShouldListenTo = source => source.Name == "DD3.CoverScope",
        Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
            options.Name == operationName ? ActivitySamplingResult.AllData : ActivitySamplingResult.None,
        ActivityStopped = activity =>
        {
            if (activity.OperationName == operationName)
                stopped.Add(activity);
        }
    };
}
