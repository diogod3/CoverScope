using DD3.CoverScope.Models.Exceptions;
using DD3.CoverScope.Models.CoverageTargets.Exceptions;
namespace DD3.CoverScope.Services.Orchestrations.CoverageLifecycle;
public partial class CoverageLifecycleOrchestrationService
{
    private async ValueTask TryCatch(Func<ValueTask> operation)
    {
        await TryCatch(async () => { await operation(); return true; });
    }

    private async ValueTask<T> TryCatch<T>(Func<ValueTask<T>> operation)
    {
        try { return await operation(); }
        catch (OperationCanceledException) { throw; }
        catch (CoverageOperationValidationException exception)
        { throw new CoverageOperationDependencyValidationException(nameof(CoverageLifecycleOrchestrationService), exception.InnerException ?? exception); }
        catch (CoverageOperationException exception)
        { throw new CoverageOperationDependencyException(nameof(CoverageLifecycleOrchestrationService), exception.InnerException ?? exception); }
        catch (CoverageTargetValidationException exception)
        { throw new CoverageOperationDependencyValidationException(nameof(CoverageLifecycleOrchestrationService), exception.InnerException ?? exception); }
        catch (CoverageTargetException exception)
        { throw new CoverageOperationDependencyException(nameof(CoverageLifecycleOrchestrationService), exception.InnerException ?? exception); }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException
            or NotSupportedException or System.Text.Json.JsonException or System.Xml.XmlException)
        { throw new CoverageOperationValidationException(nameof(CoverageLifecycleOrchestrationService), exception); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or System.ComponentModel.Win32Exception or System.Security.SecurityException)
        { throw new CoverageOperationDependencyException(nameof(CoverageLifecycleOrchestrationService), exception); }
        catch (Exception exception)
        { throw new CoverageOperationServiceException(nameof(CoverageLifecycleOrchestrationService), exception); }
    }
}
