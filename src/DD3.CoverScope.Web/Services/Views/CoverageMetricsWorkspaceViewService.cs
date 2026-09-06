using CoverageReport = DD3.CoverScope.Models.CoverageReport;
using DD3.CoverScope.Services;
using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Models;
namespace DD3.CoverScope.Services.Views;

public enum MetricSort { MostMissed, LowestCoverage, Largest, HighestCoverage, Name }
public interface ICoverageMetricsWorkspaceViewService
{
    IReadOnlyList<CoverageMetricRow> GetMetrics(CoverageReport report, PackageCoverage? project, NamespaceCoverage? namespaceItem);
    IEnumerable<CoverageMetricRow> Filter(IEnumerable<CoverageMetricRow> rows, string search, int minimumLines,
        bool onlyWithGaps, int coverageThreshold, MetricSort sortMode);
}
public partial class CoverageMetricsWorkspaceViewService(CoverageWorkspaceProjectionCache projections,
    IDiagnosticsBroker diagnosticsBroker) : ICoverageMetricsWorkspaceViewService
{
    public IReadOnlyList<CoverageMetricRow> GetMetrics(CoverageReport report, PackageCoverage? project, NamespaceCoverage? namespaceItem) =>
        Trace(() => TryCatch(() => ValueTask.FromResult(projections.GetMetrics(report, project, namespaceItem)))).GetAwaiter().GetResult();
    public IEnumerable<CoverageMetricRow> Filter(IEnumerable<CoverageMetricRow> rows, string search, int minimumLines,
        bool onlyWithGaps, int coverageThreshold, MetricSort sortMode) => Trace(() => TryCatch(() =>
    {
        ValidateRows(rows);
        return ValueTask.FromResult<IEnumerable<CoverageMetricRow>>(
            ApplyFiltersAndSort(rows, search, minimumLines, onlyWithGaps, coverageThreshold, sortMode).ToArray());
    })).GetAwaiter().GetResult();

    private static IEnumerable<CoverageMetricRow> ApplyFiltersAndSort(IEnumerable<CoverageMetricRow> rows, string search, int minimumLines, bool onlyWithGaps, int coverageThreshold, MetricSort sortMode)
    {
        var filtered = rows.Where(row =>
            (string.IsNullOrWhiteSpace(search)
                || row.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)
                || row.FullName.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
            && row.Lines.Total >= minimumLines
            && (!onlyWithGaps || row.Lines.HasGaps)
            && (coverageThreshold > 100 || (row.Lines.Total > 0 && row.Lines.Percent < coverageThreshold)));

        return sortMode switch
        {
            MetricSort.LowestCoverage => filtered
                .OrderBy(row => row.Lines.Total == 0 ? double.MaxValue : row.Lines.Percent)
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase),
            MetricSort.Largest => filtered
                .OrderByDescending(row => row.Lines.Total)
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase),
            MetricSort.HighestCoverage => filtered
                .OrderByDescending(row => row.Lines.Percent)
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase),
            MetricSort.Name => filtered.OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase),
            _ => filtered
                .OrderByDescending(row => row.Lines.Total - row.Lines.Covered)
                .ThenBy(row => row.Lines.Percent)
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
        };
    }
}
