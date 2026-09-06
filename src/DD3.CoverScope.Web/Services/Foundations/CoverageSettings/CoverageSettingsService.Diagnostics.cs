using DD3.CoverScope.Models;
using System.Runtime.CompilerServices;
namespace DD3.CoverScope.Services.Foundations.CoverageSettings;
public partial class CoverageSettingsService
{
    private async ValueTask Trace(Func<ValueTask> operation, [CallerMemberName] string memberName = "")
    {
        var activityName = $"{GetType().Name}.{memberName}";
        await diagnosticsBroker.Trace(operation, activityName);
    }

    private ValueTask<T> Trace<T>(Func<ValueTask<T>> operation, [CallerMemberName] string memberName = "") =>
        diagnosticsBroker.Trace(operation, $"{GetType().Name}.{memberName}");
}
