namespace DD3.CoverScope.Models.CoverageTargets.Exceptions;

public class NotFoundCoverageTargetException : Exception
{
    public NotFoundCoverageTargetException(string path)
        : base($"The solution or project does not exist: {path}") { }
}
