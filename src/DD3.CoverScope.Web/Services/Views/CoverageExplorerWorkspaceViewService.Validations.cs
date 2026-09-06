using DD3.CoverScope.Models;
namespace DD3.CoverScope.Services.Views;
public partial class CoverageExplorerWorkspaceViewService
{
    private static void ValidateFile(FileCoverage file) => ArgumentNullException.ThrowIfNull(file);
}
