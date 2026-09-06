namespace DD3.CoverScope.Models;

public record CoverageReport(
    string Name,
    DateTimeOffset GeneratedAt,
    string ReportPath,
    IReadOnlyList<string> SourceRoots,
    IReadOnlyList<PackageCoverage> Packages)
{
    public IReadOnlyList<FileCoverage> Files => Packages.SelectMany(x => x.Files).ToArray();
    public CoverageMetric Lines => CoverageMath.Combine(Files.Select(x => x.LineMetric));
    public CoverageMetric Branches => CoverageMath.Combine(Files.Select(x => x.BranchMetric));
    public CoverageMetric Methods => CoverageMath.Combine(
        Packages.SelectMany(x => x.Namespaces)
            .SelectMany(x => x.Classes)
            .SelectMany(x => x.Methods)
            .Select(x => x.Lines.Total > 0
                ? new CoverageMetric(x.Lines.Covered > 0 ? 1 : 0, 1)
                : new CoverageMetric(0, 0)));
}
