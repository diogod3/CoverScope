namespace DD3.CoverScope.Models.CoverageTargets.Exceptions;

public class CoverageTargetValidationException : CoverageTargetException
{
    public CoverageTargetValidationException(Exception innerException)
        : base(innerException.Message, innerException) { }
}
