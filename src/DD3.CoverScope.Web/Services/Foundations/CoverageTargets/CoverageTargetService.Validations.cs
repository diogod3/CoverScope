using DD3.CoverScope.Models.CoverageTargets;
using DD3.CoverScope.Models.CoverageTargets.Exceptions;

namespace DD3.CoverScope.Services.Foundations.CoverageTargets;

public partial class CoverageTargetService
{
    private static void ValidateTargetPath(string path, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidCoverageTargetException(
                "Choose a .sln, .slnx, or test project first.");

        if (string.IsNullOrWhiteSpace(baseDirectory)
            || !System.IO.Path.IsPathFullyQualified(baseDirectory))
            throw new InvalidCoverageTargetException(
                "The base directory must be an absolute path.");

        if (path.Contains('\0') || baseDirectory.Contains('\0'))
            throw new InvalidCoverageTargetException("The selected path is invalid.");
    }

    private static CoverageTargetType ValidateTargetType(string path)
    {
        if (!CoverageTargetFileExtensions.TryGetTargetType(path, out var type))
            throw new InvalidCoverageTargetException(
                "The target must be a .sln, .slnx, .csproj, .fsproj, or .vbproj file.");

        return type;
    }

    private static void ValidateTargetExists(bool exists, string path)
    {
        if (!exists)
            throw new NotFoundCoverageTargetException(path);
    }
}
