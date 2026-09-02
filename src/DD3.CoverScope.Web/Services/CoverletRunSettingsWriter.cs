using System.Xml.Linq;
using DD3.CoverScope.Models;

namespace DD3.CoverScope.Services;

public sealed class CoverletRunSettingsWriter
{
    public string Write(CoverageSettings settings, string destinationPath)
    {
        var configuration = new XElement("Configuration", new XElement("Format", "cobertura"));
        AddIfPresent(configuration, "Exclude", settings.Exclude);
        AddIfPresent(configuration, "ExcludeByFile", settings.ExcludeByFile);
        AddIfPresent(configuration, "ExcludeByAttribute", settings.ExcludeByAttribute);
        configuration.Add(new XElement("SkipAutoProps", settings.SkipAutoProps.ToString().ToLowerInvariant()));

        var document = new XDocument(
            new XElement("RunSettings",
                new XElement("DataCollectionRunSettings",
                    new XElement("DataCollectors",
                        new XElement("DataCollector",
                            new XAttribute("friendlyName", "XPlat code coverage"),
                            configuration)))));

        var fullPath = Path.GetFullPath(destinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        document.Save(fullPath);
        return fullPath;
    }

    private static void AddIfPresent(XElement parent, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) parent.Add(new XElement(name, value.Trim()));
    }
}
