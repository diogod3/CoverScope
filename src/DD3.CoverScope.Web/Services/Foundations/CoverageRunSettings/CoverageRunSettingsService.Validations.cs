using CoverageSettings = DD3.CoverScope.Models.CoverageSettings;
using DD3.CoverScope.Models;
namespace DD3.CoverScope.Services.Foundations.CoverageRunSettings;
public partial class CoverageRunSettingsService
{
    private static void ValidateWrite(CoverageSettings settings, string destinationPath)
    {
        ArgumentNullException.ThrowIfNull(settings); ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
    }
}
