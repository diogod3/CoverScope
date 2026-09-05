using DD3.CoverScope.Models.CoverageTargets.Exceptions;

namespace DD3.CoverScope.Services.Foundations.CoverageTargets;

public partial class CoverageTargetService
{
    private static async ValueTask<TResult> TryCatch<TResult>(
        Func<ValueTask<TResult>> operation)
    {
        try
        {
            return await operation();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidCoverageTargetException exception)
        {
            throw new CoverageTargetValidationException(exception);
        }
        catch (NotFoundCoverageTargetException exception)
        {
            throw new CoverageTargetValidationException(exception);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new CoverageTargetValidationException(
                new InvalidCoverageTargetException("The selected path is invalid.", exception));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            throw new CoverageTargetDependencyException(
                new FailedCoverageTargetDependencyException(exception));
        }
        catch (Exception exception)
        {
            throw new CoverageTargetServiceException(
                new FailedCoverageTargetServiceException(exception));
        }
    }
}
