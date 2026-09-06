using DD3.CoverScope.Models.Exceptions;
using DD3.CoverScope.Models.CoverageTargets.Exceptions;
namespace DD3.CoverScope.Services.Foundations.CoverScopeSessions;
public partial class CoverScopeSessionService
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
        { throw new CoverageOperationDependencyValidationException(nameof(CoverScopeSessionService), exception.InnerException ?? exception); }
        catch (CoverageOperationException exception)
        { throw new CoverageOperationDependencyException(nameof(CoverScopeSessionService), exception.InnerException ?? exception); }
        catch (CoverageTargetValidationException exception)
        { throw new CoverageOperationDependencyValidationException(nameof(CoverScopeSessionService), exception.InnerException ?? exception); }
        catch (CoverageTargetException exception)
        { throw new CoverageOperationDependencyException(nameof(CoverScopeSessionService), exception.InnerException ?? exception); }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException
            or NotSupportedException or System.Text.Json.JsonException or System.Xml.XmlException)
        { throw new CoverageOperationValidationException(nameof(CoverScopeSessionService), exception); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or System.ComponentModel.Win32Exception or System.Security.SecurityException)
        { throw new CoverageOperationDependencyException(nameof(CoverScopeSessionService), exception); }
        catch (Exception exception)
        { throw new CoverageOperationServiceException(nameof(CoverScopeSessionService), exception); }
    }
}
