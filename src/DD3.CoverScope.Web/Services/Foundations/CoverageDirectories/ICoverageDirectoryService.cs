using DD3.CoverScope.Models;
namespace DD3.CoverScope.Services.Foundations.CoverageDirectories;
public interface ICoverageDirectoryService
{
    SolutionBrowserSnapshot Open(string? currentSelection = null);
    SolutionBrowserSnapshot Browse(string directoryPath);
    IReadOnlyList<SolutionBrowserLocation> GetLocations();
}
