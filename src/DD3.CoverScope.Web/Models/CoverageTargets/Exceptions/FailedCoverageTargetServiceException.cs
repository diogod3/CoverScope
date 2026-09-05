namespace DD3.CoverScope.Models.CoverageTargets.Exceptions;

public class FailedCoverageTargetServiceException : Exception
{
    public FailedCoverageTargetServiceException(Exception innerException)
        : base("Target retrieval failed unexpectedly.", innerException) { }
}
