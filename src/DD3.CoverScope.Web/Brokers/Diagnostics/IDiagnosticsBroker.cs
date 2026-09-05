namespace DD3.CoverScope.Brokers.Diagnostics;

public partial interface IDiagnosticsBroker
{
    ValueTask<TResult> Trace<TResult>(
        Func<ValueTask<TResult>> operation,
        string activityName);
}
