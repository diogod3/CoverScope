using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Brokers.FileSystems;
using DD3.CoverScope.Services;
using DD3.CoverScope.Services.Foundations.CoverageTargets;

namespace DD3.CoverScope.Tests;

public partial class CoverageRunnerTests
{
    private readonly string directory =
        Path.Combine(Path.GetTempPath(), $"coverscope-rejected-run-{Guid.NewGuid():N}");

    private static CoverageRunner CreateRunner()
    {
        var targetService = new CoverageTargetService(new FileSystemBroker(), new DiagnosticsBroker());
        var store = new CoverageRunStore(new CoverageRunIdGenerator(TimeProvider.System), TimeProvider.System);
        return new CoverageRunner(
            new CoberturaReportMerger(),
            new CoverletRunSettingsWriter(),
            new TrxTestResultParser(),
            store,
            targetService);
    }
}
