namespace DD3.CoverScope.Models;

public enum CoverageRunStatus
{
    InProgress,
    Completed,
    CompletedWithTestFailures,
    Failed,
    Cancelled
}

public sealed record CoverageRunTarget(string Name, string Kind, string RelativePath);

public sealed record CoverageRunProducer(string Name, string Version);

public sealed record CoverageRunArtifact(string Kind, string Format, string Path);

public sealed record CoverageRunManifest(
    int SchemaVersion,
    string RunId,
    CoverageRunTarget Target,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    CoverageRunStatus Status,
    CoverageRunProducer Producer,
    IReadOnlyList<CoverageRunArtifact> Artifacts);
