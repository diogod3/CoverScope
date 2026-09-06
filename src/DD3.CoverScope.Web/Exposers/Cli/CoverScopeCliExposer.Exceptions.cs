using DD3.CoverScope.Models.Exceptions;
namespace DD3.CoverScope.Exposers.Cli;
public partial class CoverScopeCliExposer
{
    private async ValueTask<int> TryCatch(Func<ValueTask<int>> operation)
    {
        try { return await operation(); }
        catch (OperationCanceledException) { return 0; }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException
            or CoverageOperationValidationException or CoverageOperationDependencyValidationException)
        {
            consoleBroker.WriteError($"coverscope: {exception.Message}");
            consoleBroker.WriteError("Run 'coverscope --help' for usage.");
            return 2;
        }
        catch (Exception exception)
        {
            consoleBroker.WriteError($"coverscope: failed to start: {exception.Message}");
            return 1;
        }
    }
}
