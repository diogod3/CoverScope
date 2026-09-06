using CoverageReport = DD3.CoverScope.Models.CoverageReport;
using DD3.CoverScope.Brokers.FileSystems;
using DD3.CoverScope.Brokers.Diagnostics;
using System.Globalization;
using System.Xml.Linq;
using DD3.CoverScope.Models;

namespace DD3.CoverScope.Services.Foundations.CoverageReports;

public partial class CoverageReportService : ICoverageReportService
{
    public CoverageReport Parse(string reportPath, string? sourceRoot = null) => Trace(() => TryCatch(() =>
    {
        ValidateParse(reportPath, sourceRoot);
        return ValueTask.FromResult(ParseCore(reportPath, sourceRoot));
    })).GetAwaiter().GetResult();

    public IReadOnlyList<SourceLine> ReadSource(FileCoverage file) => Trace(() => TryCatch(() =>
    {
        ValidateReadSource(file);
        return ValueTask.FromResult(ReadSourceCore(file));
    })).GetAwaiter().GetResult();

    public void ValidateReport(string path)
    {
        Trace(() => TryCatch(() =>
        {
            ValidateParse(path, null);
            using var stream = fileSystemBroker.OpenRead(path);
            var root = XDocument.Load(stream).Root;
            if (root is null || !string.Equals(root.Name.LocalName, "coverage", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("This does not appear to be a Cobertura coverage report.");
            return ValueTask.FromResult(true);
        })).GetAwaiter().GetResult();
    }

    private readonly IFileSystemBroker fileSystemBroker;
    private readonly IDiagnosticsBroker diagnosticsBroker;

    public CoverageReportService(IFileSystemBroker fileSystemBroker, IDiagnosticsBroker diagnosticsBroker)
    {
        this.fileSystemBroker = fileSystemBroker;
        this.diagnosticsBroker = diagnosticsBroker;
    }

    private CoverageReport ParseCore(string reportPath, string? sourceRoot = null)
    {
        if (string.IsNullOrWhiteSpace(reportPath))
            throw new ArgumentException("Choose a Cobertura XML report.", nameof(reportPath));

        var absoluteReportPath = Path.GetFullPath(reportPath);
        if (!fileSystemBroker.FileExists(absoluteReportPath))
            throw new FileNotFoundException("The coverage report was not found.", absoluteReportPath);

        using var stream = fileSystemBroker.OpenRead(absoluteReportPath);
        var document = XDocument.Load(stream, LoadOptions.None);
        var root = document.Root ?? throw new InvalidDataException("The XML report has no root element.");
        if (!string.Equals(root.Name.LocalName, "coverage", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("This does not appear to be a Cobertura coverage report.");

        var reportDirectory = Path.GetDirectoryName(absoluteReportPath)!;
        var sourceRoots = root.Descendants("source")
            .Select(x => x.Value.Trim())
            .Where(x => x.Length > 0)
            .Concat(string.IsNullOrWhiteSpace(sourceRoot)
                ? Enumerable.Empty<string>()
                : new[] { Path.GetFullPath(sourceRoot) })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var parsedClasses = root.Descendants("class")
            .Select(x => ParseClass(x, sourceRoots, reportDirectory))
            .ToArray();

        var packages = parsedClasses
            .GroupBy(x => x.Package, StringComparer.OrdinalIgnoreCase)
            .Select(group => BuildPackage(group.Key, group.Select(x => x.Class).ToArray()))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var timestamp = ParseTimestamp(root.Attribute("timestamp")?.Value)
            ?? fileSystemBroker.GetLastWriteTime(absoluteReportPath);

        return new CoverageReport(
            Path.GetFileNameWithoutExtension(absoluteReportPath),
            timestamp,
            absoluteReportPath,
            sourceRoots,
            packages);
    }

    private IReadOnlyList<SourceLine> ReadSourceCore(FileCoverage file)
    {
        if (file.ResolvedPath is null || !fileSystemBroker.FileExists(file.ResolvedPath)) return [];
        var coverage = file.Lines.ToDictionary(x => x.Number);
        return fileSystemBroker.ReadAllLines(file.ResolvedPath)
            .Select((text, index) => new SourceLine(index + 1, text, coverage.GetValueOrDefault(index + 1)))
            .ToArray();
    }

    private static PackageCoverage BuildPackage(string name, IReadOnlyList<ClassCoverage> classes)
    {
        // A merged run can contain the same assembly once per test project. Collapse
        // those repeated coverage entries, but keep a partial class in two source
        // files as two distinct fragments.
        var distinctClasses = classes
            .GroupBy(
                x => $"{x.FullName}\u001f{x.RelativePath}",
                StringComparer.OrdinalIgnoreCase)
            .Select(MergeClass)
            .ToArray();
        var files = distinctClasses
            .GroupBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Select(MergeFile)
            .OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var namespaces = distinctClasses
            .GroupBy(x => x.Namespace, StringComparer.OrdinalIgnoreCase)
            .Select(x => new NamespaceCoverage(
                x.Key,
                x.OrderBy(y => y.Name, StringComparer.OrdinalIgnoreCase).ToArray()))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new PackageCoverage(name, files, namespaces);
    }

    private static ClassCoverage MergeClass(IGrouping<string, ClassCoverage> group)
    {
        var fragments = group.ToArray();
        var first = fragments[0];
        var lines = MergeLines(fragments.SelectMany(x => x.Lines));
        var methods = fragments
            .SelectMany(x => x.Methods)
            .GroupBy(
                x => $"{x.Name}\u001f{x.Signature}\u001f{x.StartLine}",
                StringComparer.Ordinal)
            .Select(MergeMethod)
            .OrderBy(x => x.StartLine)
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ToArray();

        return new ClassCoverage(
            first.Name,
            first.FullName,
            first.Namespace,
            first.RelativePath,
            fragments.Select(x => x.ResolvedPath).FirstOrDefault(x => x is not null),
            lines,
            methods);
    }

    private static MethodCoverage MergeMethod(IGrouping<string, MethodCoverage> group)
    {
        var methods = group.ToArray();
        var first = methods[0];
        return new MethodCoverage(
            first.Name,
            first.Signature,
            methods.Select(x => x.StartLine).Where(x => x > 0).DefaultIfEmpty(0).Min(),
            MergeLines(methods.SelectMany(x => x.SourceLines)));
    }

    private static FileCoverage MergeFile(IGrouping<string, ClassCoverage> group)
    {
        var classes = group.ToArray();
        var lines = MergeLines(classes.SelectMany(x => x.Lines));

        return new FileCoverage(
            group.Key,
            classes.Select(x => x.ResolvedPath).FirstOrDefault(x => x is not null),
            lines,
            classes);
    }

    private static CoverageLine[] MergeLines(IEnumerable<CoverageLine> lines) => lines
            .GroupBy(x => x.Number)
            .Select(x => new CoverageLine(
                x.Key,
                x.Max(y => y.Hits),
                x.Max(y => y.BranchesCovered),
                x.Max(y => y.BranchesTotal)))
            .OrderBy(x => x.Number)
            .ToArray();

    private ParsedClass ParseClass(XElement element, IReadOnlyList<string> roots, string reportDirectory)
    {
        var relativePath = element.Attribute("filename")?.Value?.Trim();
        if (string.IsNullOrWhiteSpace(relativePath)) relativePath = "unknown.cs";
        var package = element.Ancestors("package").FirstOrDefault()?.Attribute("name")?.Value ?? "Default";
        var fullName = element.Attribute("name")?.Value ?? Path.GetFileNameWithoutExtension(relativePath);
        var lastDot = fullName.LastIndexOf('.');
        var namespaceName = lastDot > 0 ? fullName[..lastDot] : "(global)";
        var className = lastDot >= 0 ? fullName[(lastDot + 1)..] : fullName;
        var normalizedPath = relativePath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        var lines = element.Element("lines")?.Elements("line").Select(ParseLine).ToArray() ?? [];
        var methods = element.Element("methods")?.Elements("method").Select(ParseMethod).ToArray() ?? [];

        return new ParsedClass(package, new ClassCoverage(
            className,
            fullName,
            namespaceName,
            normalizedPath,
            ResolveSourcePath(relativePath, roots, reportDirectory),
            lines,
            methods));
    }

    private static CoverageLine ParseLine(XElement element)
    {
        var (covered, total) = ParseConditionCoverage(element.Attribute("condition-coverage")?.Value);
        return new CoverageLine(Int(element.Attribute("number")?.Value), Int(element.Attribute("hits")?.Value), covered, total);
    }

    private static MethodCoverage ParseMethod(XElement element)
    {
        var lines = element.Element("lines")?.Elements("line").Select(ParseLine).ToArray() ?? [];
        return new MethodCoverage(
            element.Attribute("name")?.Value ?? "Unknown",
            element.Attribute("signature")?.Value ?? string.Empty,
            lines.FirstOrDefault()?.Number ?? 0,
            lines);
    }

    private static (int? Covered, int? Total) ParseConditionCoverage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return (null, null);
        var open = value.IndexOf('(');
        var slash = value.IndexOf('/', open + 1);
        var close = value.IndexOf(')', slash + 1);
        if (open < 0 || slash < 0 || close < 0) return (null, null);
        return (Int(value[(open + 1)..slash]), Int(value[(slash + 1)..close]));
    }

    private string? ResolveSourcePath(string path, IReadOnlyList<string> roots, string reportDirectory)
    {
        var normalized = path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        var candidates = new List<string>();
        if (Path.IsPathRooted(normalized)) candidates.Add(normalized);
        candidates.AddRange(roots.Select(root => Path.IsPathRooted(root)
            ? Path.Combine(root, normalized)
            : Path.Combine(reportDirectory, root, normalized)));
        candidates.Add(Path.Combine(reportDirectory, normalized));
        return candidates.Select(Path.GetFullPath).FirstOrDefault(fileSystemBroker.FileExists);
    }

    private static int Int(string? value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;

    private static DateTimeOffset? ParseTimestamp(string? value)
    {
        if (!long.TryParse(value, out var unix)) return null;
        if (unix > 253402300799) unix /= 1000;
        try { return DateTimeOffset.FromUnixTimeSeconds(unix); }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private record ParsedClass(string Package, ClassCoverage Class);
}
