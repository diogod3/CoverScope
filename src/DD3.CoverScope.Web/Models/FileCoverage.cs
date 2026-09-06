namespace DD3.CoverScope.Models;

public record FileCoverage(
    string RelativePath,
    string? ResolvedPath,
    IReadOnlyList<CoverageLine> Lines,
    IReadOnlyList<ClassCoverage> Classes)
{
    public string DisplayName => Path.GetFileName(RelativePath);
    public CoverageMetric LineMetric => new(Lines.Count(x => x.IsCovered), Lines.Count);
    public CoverageMetric BranchMetric => new(
        Lines.Sum(x => x.BranchesCovered ?? 0),
        Lines.Sum(x => x.BranchesTotal ?? 0));
}
