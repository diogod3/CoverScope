using System.Xml.Linq;

namespace DD3.CoverScope.Services;

public sealed class CoberturaReportMerger
{
    public string Merge(IReadOnlyList<string> reportPaths, string destinationPath)
    {
        if (reportPaths.Count == 0)
            throw new ArgumentException("At least one coverage report is required.", nameof(reportPaths));

        var documents = reportPaths
            .Select(path => (Path: Path.GetFullPath(path), Document: XDocument.Load(path)))
            .ToArray();
        var sources = documents
            .SelectMany(item => item.Document.Descendants("source")
                .Select(source => NormalizeSource(source.Value, item.Path)))
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(x => new XElement("source", x));

        var packages = documents
            .SelectMany(x => x.Document.Descendants("package"))
            .Select(x => new XElement(x));

        var merged = new XDocument(
            new XElement("coverage",
                new XAttribute("timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                new XElement("sources", sources),
                new XElement("packages", packages)));

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destinationPath))!);
        merged.Save(destinationPath);
        return Path.GetFullPath(destinationPath);
    }

    private static string NormalizeSource(string source, string reportPath)
    {
        var value = source.Trim();
        if (value.Length == 0 || Path.IsPathRooted(value)) return value;
        return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(reportPath)!, value));
    }
}
