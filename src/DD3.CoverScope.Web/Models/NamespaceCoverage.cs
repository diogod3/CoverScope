namespace DD3.CoverScope.Models;

public record NamespaceCoverage(string Name, IReadOnlyList<ClassCoverage> Classes)
{
    public CoverageMetric LineMetric => CoverageMath.Combine(Classes.Select(x => x.LineMetric));
    public CoverageMetric BranchMetric => CoverageMath.Combine(Classes.Select(x => x.BranchMetric));
}
