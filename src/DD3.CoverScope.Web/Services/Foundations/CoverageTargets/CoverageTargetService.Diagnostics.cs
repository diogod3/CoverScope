using System.Runtime.CompilerServices;

namespace DD3.CoverScope.Services.Foundations.CoverageTargets;

public partial class CoverageTargetService
{
    private async ValueTask Trace(Func<ValueTask> operation, [CallerMemberName] string memberName = "")
    {
        var activityName = $"{GetType().Name}.{memberName}";
        await diagnosticsBroker.Trace(operation, activityName);
    }

    private async ValueTask<TResult> Trace<TResult>(
        Func<ValueTask<TResult>> operation,
        [CallerMemberName] string memberName = "")
    {
        string activityName = $"{GetType().Name}.{memberName}";

        return await this.diagnosticsBroker.Trace(operation, activityName);
    }
}
