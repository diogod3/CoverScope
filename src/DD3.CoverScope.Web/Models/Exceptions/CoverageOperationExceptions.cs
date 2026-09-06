namespace DD3.CoverScope.Models.Exceptions;

public abstract class CoverageOperationException(string boundary, Exception cause) : Exception(cause.Message, cause)
{
    public string Boundary { get; } = boundary;
}
public class CoverageOperationValidationException(string boundary, Exception cause) : CoverageOperationException(boundary, cause) { }
public class CoverageOperationDependencyException(string boundary, Exception cause) : CoverageOperationException(boundary, cause) { }
public class CoverageOperationDependencyValidationException(string boundary, Exception cause) : CoverageOperationDependencyException(boundary, cause) { }
public class CoverageOperationServiceException(string boundary, Exception cause) : CoverageOperationException(boundary, cause) { }
