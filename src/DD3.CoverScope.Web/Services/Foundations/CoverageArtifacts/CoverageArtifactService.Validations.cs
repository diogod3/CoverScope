using DD3.CoverScope.Models;
namespace DD3.CoverScope.Services.Foundations.CoverageArtifacts;
public partial class CoverageArtifactService
{
    private static void ValidateDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory))
            throw new ArgumentException("An absolute artifact directory is required.");
    }
}
