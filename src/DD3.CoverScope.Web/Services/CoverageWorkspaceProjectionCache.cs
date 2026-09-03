using DD3.CoverScope.Models;

namespace DD3.CoverScope.Services;

public enum ExplorerSortMode
{
    Name,
    Lowest,
    Highest
}

public enum ExplorerCoverageFilter
{
    All,
    Gaps,
    BelowEighty,
    Untested
}

public sealed record ExplorerProjection(
    IReadOnlyList<ExplorerProject> Projects,
    int FileCount,
    int ClassCount)
{
    public static ExplorerProjection Empty { get; } = new([], 0, 0);
}

public sealed record ExplorerProject(
    PackageCoverage Project,
    IReadOnlyList<ExplorerNamespace> Namespaces);

public sealed record ExplorerNamespace(
    NamespaceCoverage Namespace,
    IReadOnlyList<ExplorerFile> Files);

public sealed record ExplorerFile(
    FileCoverage File,
    IReadOnlyList<ExplorerClass> Classes,
    CoverageMetric LineMetric);

public sealed record ExplorerClass(
    ClassCoverage Class,
    IReadOnlyList<MethodCoverage> Methods,
    bool IsPartial);

public sealed class CoverageWorkspaceProjectionCache(CoverageMetricsBuilder metrics)
{
    private CoverageReport? currentReport;
    private string? explorerFilter;
    private ExplorerCoverageFilter explorerCoverageFilter;
    private ExplorerSortMode explorerSortMode;
    private ExplorerProjection? explorerProjection;
    private IReadOnlyList<CoverageMetricRow>? projectMetrics;
    private readonly Dictionary<PackageCoverage, IReadOnlyList<CoverageMetricRow>> namespaceMetrics =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<NamespaceCoverage, IReadOnlyList<CoverageMetricRow>> classMetrics =
        new(ReferenceEqualityComparer.Instance);

    internal int ExplorerBuildCount { get; private set; }
    internal int MetricsBuildCount { get; private set; }

    public ExplorerProjection GetExplorer(
        CoverageReport? report,
        string filter,
        ExplorerCoverageFilter coverageFilter,
        ExplorerSortMode sortMode)
    {
        if (report is null)
            return ExplorerProjection.Empty;

        EnsureReport(report);
        var normalizedFilter = filter.Trim();
        if (explorerProjection is not null
            && string.Equals(explorerFilter, normalizedFilter, StringComparison.Ordinal)
            && explorerCoverageFilter == coverageFilter
            && explorerSortMode == sortMode)
        {
            return explorerProjection;
        }

        explorerFilter = normalizedFilter;
        explorerCoverageFilter = coverageFilter;
        explorerSortMode = sortMode;
        explorerProjection = BuildExplorerUncached(report, normalizedFilter, coverageFilter, sortMode);
        ExplorerBuildCount++;
        return explorerProjection;
    }

    public IReadOnlyList<CoverageMetricRow> GetMetrics(
        CoverageReport report,
        PackageCoverage? project,
        NamespaceCoverage? namespaceItem)
    {
        EnsureReport(report);

        if (project is null)
        {
            if (projectMetrics is null)
            {
                projectMetrics = metrics.BuildProjects(report);
                MetricsBuildCount++;
            }
            return projectMetrics;
        }

        if (namespaceItem is null)
        {
            if (!namespaceMetrics.TryGetValue(project, out var rows))
            {
                rows = metrics.BuildNamespaces(project);
                namespaceMetrics.Add(project, rows);
                MetricsBuildCount++;
            }
            return rows;
        }

        if (!classMetrics.TryGetValue(namespaceItem, out var classRows))
        {
            classRows = metrics.BuildClasses(project, namespaceItem);
            classMetrics.Add(namespaceItem, classRows);
            MetricsBuildCount++;
        }
        return classRows;
    }

    private void EnsureReport(CoverageReport report)
    {
        if (ReferenceEquals(currentReport, report))
            return;

        currentReport = report;
        explorerProjection = null;
        explorerFilter = null;
        projectMetrics = null;
        namespaceMetrics.Clear();
        classMetrics.Clear();
    }

    internal static ExplorerProjection BuildExplorerUncached(
        CoverageReport report,
        string filter,
        ExplorerCoverageFilter coverageFilter,
        ExplorerSortMode sortMode)
    {
        var projects = new List<ExplorerProject>();
        foreach (var project in report.Packages)
        {
            var projectTextMatch = Matches(project.Name, filter);
            var partialClassNames = project.Namespaces
                .SelectMany(x => x.Classes)
                .GroupBy(x => x.FullName, StringComparer.OrdinalIgnoreCase)
                .Where(x => x.Select(y => y.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
                .Select(x => x.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var namespaces = new List<ExplorerNamespace>();
            foreach (var namespaceItem in project.Namespaces)
            {
                var namespaceTextMatch = projectTextMatch || Matches(namespaceItem.Name, filter);
                var classes = new List<ExplorerClass>();
                foreach (var classItem in namespaceItem.Classes)
                {
                    var classTextMatch = namespaceTextMatch
                        || Matches(classItem.Name, filter)
                        || Matches(classItem.FullName, filter)
                        || Matches(classItem.RelativePath, filter);
                    var methods = Sort(
                            classItem.Methods.Where(method =>
                                (classTextMatch || Matches(method.Name, filter) || Matches(method.Signature, filter))
                                && Passes(method.Lines, coverageFilter)),
                            x => x.Name,
                            x => x.Lines,
                            sortMode)
                        .ToArray();
                    if ((classTextMatch && Passes(classItem.LineMetric, coverageFilter)) || methods.Length > 0)
                        classes.Add(new ExplorerClass(classItem, methods, partialClassNames.Contains(classItem.FullName)));
                }

                var files = classes
                    .GroupBy(x => x.Class.RelativePath, StringComparer.OrdinalIgnoreCase)
                    .Select(group =>
                    {
                        var file = project.Files.First(x =>
                            string.Equals(x.RelativePath, group.Key, StringComparison.OrdinalIgnoreCase));
                        var fileClasses = Sort(
                                group,
                                x => x.Class.Name,
                                x => x.Class.LineMetric,
                                sortMode)
                            .ToArray();
                        return new ExplorerFile(file, fileClasses, LineMetric(fileClasses.Select(x => x.Class)));
                    });
                var sortedFiles = Sort(
                        files,
                        x => x.File.RelativePath,
                        x => x.LineMetric,
                        sortMode)
                    .ToArray();
                if (sortedFiles.Length > 0)
                    namespaces.Add(new ExplorerNamespace(namespaceItem, sortedFiles));
            }

            if (namespaces.Count > 0)
            {
                projects.Add(new ExplorerProject(
                    project,
                    Sort(
                            namespaces,
                            x => x.Namespace.Name,
                            x => x.Namespace.LineMetric,
                            sortMode)
                        .ToArray()));
            }
        }

        var sortedProjects = Sort(
                projects,
                x => x.Project.Name,
                x => x.Project.LineMetric,
                sortMode)
            .ToArray();
        return new ExplorerProjection(
            sortedProjects,
            sortedProjects.Sum(x => x.Namespaces.Sum(y => y.Files.Count)),
            sortedProjects.Sum(x => x.Namespaces.Sum(y => y.Files.Sum(z => z.Classes.Count))));
    }

    private static bool Matches(string value, string filter) =>
        filter.Length == 0 || value.Contains(filter, StringComparison.OrdinalIgnoreCase);

    private static CoverageMetric LineMetric(IEnumerable<ClassCoverage> classes)
    {
        var lines = classes
            .SelectMany(x => x.Lines)
            .GroupBy(x => x.Number)
            .Select(x => x.Any(y => y.IsCovered));
        var total = 0;
        var covered = 0;
        foreach (var isCovered in lines)
        {
            total++;
            if (isCovered)
                covered++;
        }
        return new CoverageMetric(covered, total);
    }

    private static bool Passes(CoverageMetric metric, ExplorerCoverageFilter coverageFilter) =>
        coverageFilter switch
        {
            ExplorerCoverageFilter.Gaps => metric.HasGaps,
            ExplorerCoverageFilter.BelowEighty => metric.Total > 0 && metric.Percent < 80,
            ExplorerCoverageFilter.Untested => metric.Total > 0 && metric.Covered == 0,
            _ => true
        };

    private static IEnumerable<T> Sort<T>(
        IEnumerable<T> items,
        Func<T, string> name,
        Func<T, CoverageMetric> metric,
        ExplorerSortMode sortMode) =>
        sortMode switch
        {
            ExplorerSortMode.Lowest => items
                .OrderBy(x => metric(x).Percent)
                .ThenBy(name, StringComparer.OrdinalIgnoreCase),
            ExplorerSortMode.Highest => items
                .OrderByDescending(x => metric(x).Percent)
                .ThenBy(name, StringComparer.OrdinalIgnoreCase),
            _ => items.OrderBy(name, StringComparer.OrdinalIgnoreCase)
        };
}
