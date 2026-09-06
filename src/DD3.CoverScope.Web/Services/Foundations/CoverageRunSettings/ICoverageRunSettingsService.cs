using CoverageSettings = DD3.CoverScope.Models.CoverageSettings;
using DD3.CoverScope.Models;
namespace DD3.CoverScope.Services.Foundations.CoverageRunSettings;
public interface ICoverageRunSettingsService
{
    string Write(CoverageSettings settings, string destinationPath);
}
