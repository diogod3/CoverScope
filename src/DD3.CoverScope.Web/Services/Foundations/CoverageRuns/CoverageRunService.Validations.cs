using DD3.CoverScope.Models;
namespace DD3.CoverScope.Services.Foundations.CoverageRuns;
public partial class CoverageRunService
{
    private void ValidateArtifacts(
        string runDirectory,
        IEnumerable<CoverageRunArtifact> artifacts,
        bool requireExisting = false)
    {
        var fullRunDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(runDirectory));
        var runPrefix = fullRunDirectory + Path.DirectorySeparatorChar;
        foreach (var artifact in artifacts)
        {
            ValidateRelativePath(artifact.Path, "artifact");
            var resolved = Path.GetFullPath(artifact.Path, fullRunDirectory);
            if (!resolved.StartsWith(runPrefix, PathComparison))
                throw new InvalidDataException($"Artifact path '{artifact.Path}' escapes the run directory.");
            if (requireExisting && !fileSystemBroker.FileExists(resolved))
                throw new FileNotFoundException($"The run artifact '{artifact.Path}' was not written successfully.", resolved);
        }
    }

    private static void ValidateRelativePath(string path, string description)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
            throw new InvalidDataException($"The {description} path '{path}' must be relative.");
        if (path.Split('/', '\\')
            .Any(segment => segment == ".."))
            throw new InvalidDataException($"The {description} path '{path}' cannot contain parent traversal.");
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;


    private static void ValidateRun(CoverageRun run)
    {
        if (run.Id == Guid.Empty || run.Id.Version != 7 || run.Settings is null)
            throw new InvalidDataException("A run requires a UUIDv7 ID and effective settings.");
        if (!Enum.IsDefined(run.Status) || !Enum.IsDefined(run.Target.Type))
            throw new InvalidDataException("Unknown run status or target type.");
        bool terminal = run.Status is CoverageRunStatus.Succeeded or CoverageRunStatus.TestsFailed
            or CoverageRunStatus.ExecutionFailed or CoverageRunStatus.Cancelled;
        if (terminal != (run.CompletedAt is not null))
            throw new InvalidDataException("Only terminal runs have a completion timestamp.");
        if (run.StartedAt == default || run.StartedAt.Offset != TimeSpan.Zero
            || (run.CompletedAt is { } completed && (completed.Offset != TimeSpan.Zero || completed < run.StartedAt)))
            throw new InvalidDataException("Run timestamps must be UTC and completion cannot precede the start.");
    }
}
