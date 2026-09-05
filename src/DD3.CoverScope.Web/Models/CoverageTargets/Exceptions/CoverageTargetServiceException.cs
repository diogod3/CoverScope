namespace DD3.CoverScope.Models.CoverageTargets.Exceptions;

public class CoverageTargetServiceException : CoverageTargetException
{
    public CoverageTargetServiceException(FailedCoverageTargetServiceException innerException)
        : base("The solution or project could not be retrieved.", innerException) { }
}
