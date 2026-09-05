namespace DD3.CoverScope.Models.CoverageTargets.Exceptions;

public abstract class CoverageTargetException : Exception
{
    protected CoverageTargetException(string message, Exception innerException)
        : base(message, innerException) { }
}
