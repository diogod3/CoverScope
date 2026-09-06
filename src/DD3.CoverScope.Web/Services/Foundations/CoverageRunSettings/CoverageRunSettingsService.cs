using CoverageSettings = DD3.CoverScope.Models.CoverageSettings;
using DD3.CoverScope.Brokers.FileSystems;
using DD3.CoverScope.Brokers.Diagnostics;
using System.Xml.Linq;
using DD3.CoverScope.Models;

namespace DD3.CoverScope.Services.Foundations.CoverageRunSettings;

public partial class CoverageRunSettingsService : ICoverageRunSettingsService
{
    public string Write(CoverageSettings settings, string destinationPath) => Trace(() => TryCatch(() =>
    {
        ValidateWrite(settings, destinationPath);
        return ValueTask.FromResult(WriteCore(settings, destinationPath));
    })).GetAwaiter().GetResult();

    private readonly IFileSystemBroker fileSystemBroker;
    private readonly IDiagnosticsBroker diagnosticsBroker;

    public CoverageRunSettingsService(IFileSystemBroker fileSystemBroker, IDiagnosticsBroker diagnosticsBroker)
    {
        this.fileSystemBroker = fileSystemBroker;
        this.diagnosticsBroker = diagnosticsBroker;
    }

    private string WriteCore(CoverageSettings settings, string destinationPath)
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
        fileSystemBroker.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        fileSystemBroker.WriteAllText(fullPath, document.ToString());
        return fullPath;
    }

    private static void AddIfPresent(XElement parent, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) parent.Add(new XElement(name, value.Trim()));
    }
}
