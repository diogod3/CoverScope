namespace DD3.CoverScope.Models.CoverageTargets.Exceptions;

public class CoverageTargetDependencyException : CoverageTargetException
{
    public CoverageTargetDependencyException(FailedCoverageTargetDependencyException innerException)
        : base("The solution or project could not be accessed.", innerException) { }
}
