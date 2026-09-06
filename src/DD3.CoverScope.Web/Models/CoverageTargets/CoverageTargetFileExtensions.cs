using DD3.CoverScope.Services.Foundations.CoverageTargets;
namespace DD3.CoverScope.Models.CoverageTargets;

// Shared format classification for target validation and browser filtering.
// File existence and other external checks belong to CoverageTargetService.
internal static class CoverageTargetFileExtensions
{
    internal static bool TryGetTargetType(string path, out CoverageTargetType type)
    {
        switch (System.IO.Path.GetExtension(path).ToLowerInvariant())
        {
            case ".sln":
            case ".slnx":
                type = CoverageTargetType.Solution;
                return true;
            case ".csproj":
            case ".fsproj":
            case ".vbproj":
                type = CoverageTargetType.Project;
                return true;
            default:
                type = default;
                return false;
        }
    }
}
