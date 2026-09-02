using System.Xml.Linq;
using DD3.CoverScope.Models;
using DD3.CoverScope.Services;
using Xunit;

namespace DD3.CoverScope.Tests;

public sealed class CoverletRunSettingsWriterTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"coverscope-settings-{Guid.NewGuid():N}");

    public CoverletRunSettingsWriterTests() => Directory.CreateDirectory(directory);

    [Fact]
    public void Write_EmitsConfiguredCoverletExclusions()
    {
        var path = Path.Combine(directory, "coverage.runsettings");
        new CoverletRunSettingsWriter().Write(new CoverageSettings
        {
            Exclude = "[*Tests]*",
            ExcludeByFile = "**/*.g.cs",
            ExcludeByAttribute = "GeneratedCodeAttribute",
            SkipAutoProps = true
        }, path);

        var configuration = XDocument.Load(path).Descendants("Configuration").Single();

        Assert.Equal("cobertura", configuration.Element("Format")?.Value);
        Assert.Equal("[*Tests]*", configuration.Element("Exclude")?.Value);
        Assert.Equal("**/*.g.cs", configuration.Element("ExcludeByFile")?.Value);
        Assert.Equal("GeneratedCodeAttribute", configuration.Element("ExcludeByAttribute")?.Value);
        Assert.Equal("true", configuration.Element("SkipAutoProps")?.Value);
    }

    public void Dispose()
    {
        try { Directory.Delete(directory, recursive: true); }
        catch (IOException) { }
    }
}
