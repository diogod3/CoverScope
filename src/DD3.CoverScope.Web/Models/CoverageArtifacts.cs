namespace DD3.CoverScope.Models;
public record CoverageArtifacts(IReadOnlyList<string> CoveragePaths, IReadOnlyList<string> TestResultPaths)
{
    public static CoverageArtifacts Empty { get; } = new([], []);
}
public record CoverageCollectionResult(int ExitCode, string Output, CoverageArtifacts Artifacts);
public record CoverageResults(string? ReportPath, TestRunSummary? Tests, int ReportCount);
