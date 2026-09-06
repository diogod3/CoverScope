using CoverageSettings = DD3.CoverScope.Models.CoverageSettings;
using DD3.CoverScope.Models;
namespace DD3.CoverScope.Services.Foundations.CoverageSettings;
public partial class CoverageSettingsService
{
    private static void ValidateSettings(CoverageSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.Exclude is null || settings.ExcludeByFile is null || settings.ExcludeByAttribute is null)
            throw new ArgumentException("Coverage exclusion settings cannot be null.");
    }
}
