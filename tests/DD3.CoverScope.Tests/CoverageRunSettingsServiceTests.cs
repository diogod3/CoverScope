using System.Xml.Linq;
using DD3.CoverScope.Models;
using DD3.CoverScope.Services;
using Xunit;

namespace DD3.CoverScope.Tests;

public partial class CoverageRunSettingsServiceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"coverscope-settings-{Guid.NewGuid():N}");

    public CoverageRunSettingsServiceTests() => Directory.CreateDirectory(directory);

    public void Dispose()
    {
        try { Directory.Delete(directory, recursive: true); }
        catch (IOException) { }
    }
}
