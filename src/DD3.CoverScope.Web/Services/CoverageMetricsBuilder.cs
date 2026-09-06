using CoverageReport = DD3.CoverScope.Models.CoverageReport;
using DD3.CoverScope.Models;

namespace DD3.CoverScope.Services;

public class CoverageMetricsBuilder
{
    public IReadOnlyList<CoverageMetricRow> BuildProjects(CoverageReport report) => report.Packages
        .Select(project =>
        {
            var classes = project.Namespaces.SelectMany(x => x.Classes).ToArray();
            var aggregate = Aggregate(classes);
            return new CoverageMetricRow(
                CoverageMetricScope.Project,
                project.Name,
                project.Name,
                aggregate.Lines,
                aggregate.Branches,
                aggregate.Methods,
                aggregate.FileCount,
                project.Namespaces.Count,
                project,
                null,
                []);
        })
        .ToArray();

    public IReadOnlyList<CoverageMetricRow> BuildNamespaces(PackageCoverage project) => project.Namespaces
        .Select(namespaceItem =>
        {
            var aggregate = Aggregate(namespaceItem.Classes);
            var logicalClassCount = namespaceItem.Classes
                .Select(x => x.FullName)
                .Distinct(StringComparer.Ordinal)
                .Count();
            return new CoverageMetricRow(
                CoverageMetricScope.Namespace,
                namespaceItem.Name,
                namespaceItem.Name,
                aggregate.Lines,
                aggregate.Branches,
                aggregate.Methods,
                aggregate.FileCount,
                logicalClassCount,
                project,
                namespaceItem,
                []);
        })
        .ToArray();

    public IReadOnlyList<CoverageMetricRow> BuildClasses(
        PackageCoverage project,
        NamespaceCoverage namespaceItem) => namespaceItem.Classes
        .GroupBy(x => x.FullName, StringComparer.Ordinal)
        .Select(group =>
        {
            var fragments = group.OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray();
            var aggregate = Aggregate(fragments);
            return new CoverageMetricRow(
                CoverageMetricScope.Class,
                fragments[0].Name,
                fragments[0].FullName,
                aggregate.Lines,
                aggregate.Branches,
                aggregate.Methods,
                aggregate.FileCount,
                aggregate.Methods.Total,
                project,
                namespaceItem,
                fragments);
        })
        .ToArray();

    private static AggregateMetrics Aggregate(IEnumerable<ClassCoverage> classes)
    {
        var fragments = classes.ToArray();
        var lines = fragments
            .SelectMany(fragment => fragment.Lines.Select(line => new FileLine(fragment.RelativePath, line)))
            .GroupBy(x => $"{x.Path}\u001f{x.Line.Number}", StringComparer.OrdinalIgnoreCase)
            .Select(group => new CoverageLine(
                group.First().Line.Number,
                group.Max(x => x.Line.Hits),
                group.Max(x => x.Line.BranchesCovered),
                group.Max(x => x.Line.BranchesTotal)))
            .ToArray();
        var methods = fragments
            .SelectMany(fragment => fragment.Methods.Select(method => new FileMethod(fragment.RelativePath, fragment.FullName, method)))
            .GroupBy(
                x => $"{x.Path.ToUpperInvariant()}\u001f{x.ClassName}\u001f{x.Method.Name}\u001f{x.Method.Signature}\u001f{x.Method.StartLine}",
                StringComparer.Ordinal)
            .Select(group => group.Any(x => x.Method.Lines.Covered > 0))
            .ToArray();

        return new AggregateMetrics(
            new CoverageMetric(lines.Count(x => x.IsCovered), lines.Length),
            new CoverageMetric(
                lines.Sum(x => x.BranchesCovered ?? 0),
                lines.Sum(x => x.BranchesTotal ?? 0)),
            new CoverageMetric(methods.Count(x => x), methods.Length),
            fragments.Select(x => x.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    private record FileLine(string Path, CoverageLine Line);
    private record FileMethod(string Path, string ClassName, MethodCoverage Method);
    private record AggregateMetrics(
        CoverageMetric Lines,
        CoverageMetric Branches,
        CoverageMetric Methods,
        int FileCount);
}
