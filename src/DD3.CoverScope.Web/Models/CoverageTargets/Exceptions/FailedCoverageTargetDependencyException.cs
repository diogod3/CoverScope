namespace DD3.CoverScope.Models.CoverageTargets.Exceptions;

public class FailedCoverageTargetDependencyException : Exception
{
    public FailedCoverageTargetDependencyException(Exception innerException)
        : base("The filesystem could not be accessed to retrieve the target.", innerException) { }
}
