using CoverageReport = DD3.CoverScope.Models.CoverageReport;
using DD3.CoverScope.Services;
using DD3.CoverScope.Services.Foundations.CoverageReports;
using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Brokers.ElementNavigations;
using DD3.CoverScope.Models;
namespace DD3.CoverScope.Services.Views;

public interface ICoverageExplorerWorkspaceViewService
{
    ExplorerProjection Project(CoverageReport? report, string filter, ExplorerCoverageFilter coverageFilter, ExplorerSortMode sort);
    IReadOnlyList<SourceLine> ReadSource(FileCoverage file);
    ValueTask ScrollToLineAsync(int line);
}
public partial class CoverageExplorerWorkspaceViewService(CoverageWorkspaceProjectionCache projections,
    ICoverageReportService parser, IElementNavigationBroker navigationBroker,
    IDiagnosticsBroker diagnosticsBroker) : ICoverageExplorerWorkspaceViewService
{
    public ExplorerProjection Project(CoverageReport? report, string filter, ExplorerCoverageFilter coverageFilter, ExplorerSortMode sort) =>
        Trace(() => TryCatch(() => ValueTask.FromResult(projections.GetExplorer(report, filter, coverageFilter, sort)))).GetAwaiter().GetResult();
    public IReadOnlyList<SourceLine> ReadSource(FileCoverage file) => Trace(() => TryCatch(() =>
    {
        ValidateFile(file);
        return ValueTask.FromResult(parser.ReadSource(file));
    })).GetAwaiter().GetResult();
    public async ValueTask ScrollToLineAsync(int line)
    {
        await Trace(() => TryCatch(async () =>
        {
            if (line > 0) await navigationBroker.ScrollToAsync($"line-{line}");
            return true;
        }));
    }
}
