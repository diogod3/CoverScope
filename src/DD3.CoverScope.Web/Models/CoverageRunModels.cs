using DD3.CoverScope.Models.CoverageTargets;

namespace DD3.CoverScope.Models;

public enum CoverageRunStatus { Created, Running, CancellationRequested, Succeeded, TestsFailed, ExecutionFailed, Cancelled }

public record CoverageRunTarget(string Name, CoverageTargetType Type, string RelativePath);

public record CoverageRunSettings(string Exclude, string ExcludeByFile, string ExcludeByAttribute, bool SkipAutoProps)
{
    public static CoverageRunSettings From(CoverageSettings settings) =>
        new(settings.Exclude.Trim(), settings.ExcludeByFile.Trim(), settings.ExcludeByAttribute.Trim(), settings.SkipAutoProps);

    public CoverageSettings ToSettings() => new()
    {
        Exclude = Exclude, ExcludeByFile = ExcludeByFile,
        ExcludeByAttribute = ExcludeByAttribute, SkipAutoProps = SkipAutoProps
    };
}

public record CoverageRunProducer(string Name, string Version);
public record CoverageRunArtifact(string Kind, string Format, string Path);

public record CoverageRun(
    int SchemaVersion, Guid Id, CoverageRunTarget Target, CoverageRunSettings Settings,
    DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, CoverageRunStatus Status,
    CoverageRunProducer Producer, IReadOnlyList<CoverageRunArtifact> Artifacts);
