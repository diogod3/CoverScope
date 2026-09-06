using DD3.CoverScope.Models;
namespace DD3.CoverScope.Services.Views;
public partial class CoverageMetricsWorkspaceViewService
{
    private static void ValidateRows(IEnumerable<CoverageMetricRow> rows) => ArgumentNullException.ThrowIfNull(rows);
}
