using DD3.CoverScope.Models.Exceptions;
using DD3.CoverScope.Models.CoverageTargets.Exceptions;
namespace DD3.CoverScope.Services.Coordinations.CoverageRuns;
public partial class CoverageRunCoordinationService
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
        { throw new CoverageOperationDependencyValidationException(nameof(CoverageRunCoordinationService), exception.InnerException ?? exception); }
        catch (CoverageOperationException exception)
        { throw new CoverageOperationDependencyException(nameof(CoverageRunCoordinationService), exception.InnerException ?? exception); }
        catch (CoverageTargetValidationException exception)
        { throw new CoverageOperationDependencyValidationException(nameof(CoverageRunCoordinationService), exception.InnerException ?? exception); }
        catch (CoverageTargetException exception)
        { throw new CoverageOperationDependencyException(nameof(CoverageRunCoordinationService), exception.InnerException ?? exception); }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException
            or NotSupportedException or System.Text.Json.JsonException or System.Xml.XmlException)
        { throw new CoverageOperationValidationException(nameof(CoverageRunCoordinationService), exception); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or System.ComponentModel.Win32Exception or System.Security.SecurityException)
        { throw new CoverageOperationDependencyException(nameof(CoverageRunCoordinationService), exception); }
        catch (Exception exception)
        { throw new CoverageOperationServiceException(nameof(CoverageRunCoordinationService), exception); }
    }
}
