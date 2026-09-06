namespace DD3.CoverScope.Models;

public record MethodCoverage(
    string Name,
    string Signature,
    int StartLine,
    IReadOnlyList<CoverageLine> SourceLines)
{
    public CoverageMetric Lines => new(SourceLines.Count(x => x.IsCovered), SourceLines.Count);
    public CoverageMetric Branches => new(
        SourceLines.Sum(x => x.BranchesCovered ?? 0),
        SourceLines.Sum(x => x.BranchesTotal ?? 0));
}
