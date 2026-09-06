namespace DD3.CoverScope.Models;

public record PackageCoverage(
    string Name,
    IReadOnlyList<FileCoverage> Files,
    IReadOnlyList<NamespaceCoverage> Namespaces)
{
    public CoverageMetric LineMetric => CoverageMath.Combine(Files.Select(x => x.LineMetric));
    public CoverageMetric BranchMetric => CoverageMath.Combine(Files.Select(x => x.BranchMetric));
}
