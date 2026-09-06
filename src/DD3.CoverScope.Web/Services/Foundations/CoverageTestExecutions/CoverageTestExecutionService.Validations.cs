namespace DD3.CoverScope.Services.Foundations.CoverageTestExecutions;
public partial class CoverageTestExecutionService
{
    private static void ValidatePaths(params string[] paths)
    {
        if (paths.Any(path => string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)))
            throw new ArgumentException("Test execution requires resolved absolute paths.");
    }
}
