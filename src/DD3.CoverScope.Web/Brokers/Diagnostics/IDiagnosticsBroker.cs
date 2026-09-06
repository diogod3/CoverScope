namespace DD3.CoverScope.Brokers.Diagnostics;

public partial interface IDiagnosticsBroker
{
    async ValueTask Trace(Func<ValueTask> operation, string activityName)
    {
        await Trace(async () => { await operation(); return true; }, activityName);
    }

    ValueTask<TResult> Trace<TResult>(
        Func<ValueTask<TResult>> operation,
        string activityName);
}
