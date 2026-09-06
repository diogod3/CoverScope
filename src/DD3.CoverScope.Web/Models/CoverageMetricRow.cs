namespace DD3.CoverScope.Models;

public record CoverageMetricRow(
    CoverageMetricScope Scope,
    string Name,
    string FullName,
    CoverageMetric Lines,
    CoverageMetric Branches,
    CoverageMetric Methods,
    int FileCount,
    int ChildCount,
    PackageCoverage Project,
    NamespaceCoverage? Namespace,
    IReadOnlyList<ClassCoverage> ClassFragments);
