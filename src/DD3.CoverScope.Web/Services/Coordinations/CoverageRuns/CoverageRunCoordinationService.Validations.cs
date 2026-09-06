using CoverageSettings = DD3.CoverScope.Models.CoverageSettings;
using DD3.CoverScope.Models;
namespace DD3.CoverScope.Services.Coordinations.CoverageRuns;
public partial class CoverageRunCoordinationService
{
    private static void ValidateSettings(CoverageSettings settings) => ArgumentNullException.ThrowIfNull(settings);
}
