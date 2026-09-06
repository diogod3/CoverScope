using DD3.CoverScope.Brokers.DateTimes;
using DD3.CoverScope.Brokers.FileSystems;
using DD3.CoverScope.Brokers.Diagnostics;
using System.Xml.Linq;

namespace DD3.CoverScope.Services.Foundations.CoverageReports;

public partial class CoverageReportMergeService : ICoverageReportMergeService
{
    public string Merge(IReadOnlyList<string> reportPaths, string destinationPath) => Trace(() => TryCatch(() =>
    {
        ValidateMerge(reportPaths, destinationPath);
        return ValueTask.FromResult(MergeCore(reportPaths, destinationPath));
    })).GetAwaiter().GetResult();

    private readonly IDateTimeBroker dateTimeBroker;
    private readonly IFileSystemBroker fileSystemBroker;
    private readonly IDiagnosticsBroker diagnosticsBroker;

    public CoverageReportMergeService(IFileSystemBroker fileSystemBroker, IDiagnosticsBroker diagnosticsBroker, IDateTimeBroker dateTimeBroker)
    {
        this.dateTimeBroker = dateTimeBroker;
        this.fileSystemBroker = fileSystemBroker;
        this.diagnosticsBroker = diagnosticsBroker;
    }

    private string MergeCore(IReadOnlyList<string> reportPaths, string destinationPath)
    {
        if (reportPaths.Count == 0)
            throw new ArgumentException("At least one coverage report is required.", nameof(reportPaths));

        var documents = reportPaths
            .Select(path => (Path: Path.GetFullPath(path), Document: XDocument.Parse(fileSystemBroker.ReadAllText(path))))
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
                new XAttribute("timestamp", dateTimeBroker.GetCurrentDateTime().ToUnixTimeSeconds()),
                new XElement("sources", sources),
                new XElement("packages", packages)));

        fileSystemBroker.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destinationPath))!);
        fileSystemBroker.WriteAllText(destinationPath, merged.ToString());
        return Path.GetFullPath(destinationPath);
    }

    private static string NormalizeSource(string source, string reportPath)
    {
        var value = source.Trim();
        if (value.Length == 0 || Path.IsPathRooted(value)) return value;
        return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(reportPath)!, value));
    }
}
