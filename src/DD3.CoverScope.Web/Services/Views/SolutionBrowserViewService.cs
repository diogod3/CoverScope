using DD3.CoverScope.Models;
using DD3.CoverScope;
using DD3.CoverScope.Services.Foundations.CoverageDirectories;
using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Models.CoverageTargets.Exceptions;
using DD3.CoverScope.Services.Foundations.CoverageTargets;
namespace DD3.CoverScope.Services.Views;

public interface ISolutionBrowserViewService
{
    SolutionBrowserSnapshot Open(string? selection = null);
    SolutionBrowserSnapshot Browse(string path);
    IReadOnlyList<SolutionBrowserLocation> GetLocations();
    ValueTask<SolutionSelectionResult> ValidateSelectionAsync(string path);
}
public partial class SolutionBrowserViewService(ICoverageDirectoryService browser,
    ICoverageTargetService targetService, CoverScopeLaunchContext launchContext,
    IDiagnosticsBroker diagnosticsBroker) : ISolutionBrowserViewService
{
    public SolutionBrowserSnapshot Open(string? selection = null) =>
        Trace(() => TryCatch(() => ValueTask.FromResult(browser.Open(selection)))).GetAwaiter().GetResult();
    public SolutionBrowserSnapshot Browse(string path) => Trace(() => TryCatch(() =>
    {
        ValidatePath(path);
        return ValueTask.FromResult(browser.Browse(path));
    })).GetAwaiter().GetResult();
    public IReadOnlyList<SolutionBrowserLocation> GetLocations() =>
        Trace(() => TryCatch(() => ValueTask.FromResult(browser.GetLocations()))).GetAwaiter().GetResult();
    public ValueTask<SolutionSelectionResult> ValidateSelectionAsync(string path) => Trace(() => TryCatch(async () =>
    {
        try
        {
            var target = await targetService.RetrieveCoverageTargetAsync(path, launchContext.InvocationDirectory);
            return new SolutionSelectionResult(true, target.Path, "Solution selected.");
        }
        catch (CoverageTargetException exception) { return new SolutionSelectionResult(false, null, exception.Message); }
    }));
}
