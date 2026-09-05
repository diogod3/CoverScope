namespace DD3.CoverScope.Models.CoverageTargets.Exceptions;

public class InvalidCoverageTargetException : Exception
{
    public InvalidCoverageTargetException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
