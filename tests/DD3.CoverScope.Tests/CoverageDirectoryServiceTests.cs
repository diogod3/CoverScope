using DD3.CoverScope;
using DD3.CoverScope.Services.Foundations.CoverageDirectories;
using DD3.CoverScope.Services.Views;
using DD3.CoverScope.Services;
using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Brokers.FileSystems;
using DD3.CoverScope.Services.Foundations.CoverageTargets;
using Xunit;

namespace DD3.CoverScope.Tests;

public partial class CoverageDirectoryServiceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"coverscope-browser-{Guid.NewGuid():N}");
    private readonly SolutionBrowserViewService browser;

    public CoverageDirectoryServiceTests()
    {
        Directory.CreateDirectory(directory);
        browser = CreateBrowser();
    }

    private SolutionBrowserViewService CreateBrowser()
    {
        var fileSystemBroker = new FileSystemBroker();
        var targetService = new CoverageTargetService(fileSystemBroker, new DiagnosticsBroker());
        return new SolutionBrowserViewService(new CoverageDirectoryService(directory, fileSystemBroker, new DiagnosticsBroker()),
            targetService, new CoverScopeLaunchContext(directory, null), new DiagnosticsBroker());
    }

    public void Dispose()
    {
        try { Directory.Delete(directory, recursive: true); }
        catch (IOException) { }
    }
}
