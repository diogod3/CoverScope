namespace DD3.CoverScope.Models;

public record ClassCoverage(
    string Name,
    string FullName,
    string Namespace,
    string RelativePath,
    string? ResolvedPath,
    IReadOnlyList<CoverageLine> Lines,
    IReadOnlyList<MethodCoverage> Methods)
{
    public CoverageMetric LineMetric => new(Lines.Count(x => x.IsCovered), Lines.Count);
    public CoverageMetric BranchMetric => new(
        Lines.Sum(x => x.BranchesCovered ?? 0),
        Lines.Sum(x => x.BranchesTotal ?? 0));
}
