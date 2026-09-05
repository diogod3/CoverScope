using System.Diagnostics;

namespace DD3.CoverScope.Brokers.Diagnostics;

public partial class DiagnosticsBroker : IDiagnosticsBroker
{
    private static readonly ActivitySource Source = new("DD3.CoverScope");

    public async ValueTask<TResult> Trace<TResult>(
        Func<ValueTask<TResult>> operation,
        string activityName)
    {
        using var activity = Source.StartActivity(activityName);

        try
        {
            return await operation();
        }
        catch (OperationCanceledException)
        {
            activity?.SetTag("operation.cancelled", true);
            throw;
        }
        catch (Exception)
        {
            // Do not attach exception messages or user paths to telemetry.
            activity?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
    }
}
