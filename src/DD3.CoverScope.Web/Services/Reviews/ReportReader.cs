using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using DD3.CoverScope.Models.Reviews;

namespace DD3.CoverScope.Services.Reviews;

public sealed class ReportReader
{
    public IReadOnlyList<TestExecution> ReadTests(string xml, string scope)
    {
        var document = XDocument.Parse(xml);
        var definitions = document.Descendants().Where(x => x.Name.LocalName == "UnitTest")
            .GroupBy(x => Attribute(x, "id"), StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First());
        return document.Descendants().Where(x => x.Name.LocalName == "UnitTestResult").Select(result =>
        {
            definitions.TryGetValue(Attribute(result, "testId"), out var definition);
            var method = definition?.Descendants().FirstOrDefault(x => x.Name.LocalName == "TestMethod");
            TimeSpan.TryParse(Attribute(result, "duration"), CultureInfo.InvariantCulture, out var duration);
            return new TestExecution(scope + ":" + Attribute(result, "executionId"), Attribute(result, "testName"),
                Attribute(method, "className"), Attribute(method, "name"), Attribute(method, "codeBase"),
                Attribute(result, "outcome"), Value(result, "Message"), Value(result, "StackTrace"),
                Value(result, "StdOut") + "\n" + Value(result, "StdErr"), duration.TotalSeconds);
        }).ToArray();
    }

    public IReadOnlyList<FormattingFinding> ReadFormatting(string json, string root)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array) { throw new InvalidDataException("Unrecognized formatter report."); }
        var results = new List<FormattingFinding>();
        foreach (var file in document.RootElement.EnumerateArray())
        {
            var path = Text(file, "FilePath");
            if (!file.TryGetProperty("FileChanges", out var changes) || changes.ValueKind != JsonValueKind.Array)
            { throw new InvalidDataException("Formatter report has no recognized change list."); }
            foreach (var change in changes.EnumerateArray())
            {
                results.Add(new(NormalizePath(path, root), Number(change, "LineNumber"), Number(change, "CharNumber"),
                    Text(change, "DiagnosticId"), Text(change, "FormatDescription"), "Reported by formatter"));
            }
        }
        return results;
    }

    public CoverageEvidence ReadCoverageArtifacts(IReadOnlyList<(string Name, string Json)> artifacts, string root)
    {
        // VSTest can copy a collector attachment into the TRX results directory.
        // Only identical report contents are duplicates; equal coverage totals are not proof.
        var originals = new Dictionary<string, string>(StringComparer.Ordinal);
        var provenance = new List<CoverageArtifactEvidence>();
        var unique = new List<(string Name, string Json)>();
        var compatibility = new CoverageReportCompatibility();
        string? conflict = null;
        foreach (var artifact in artifacts)
        {
            if (originals.TryGetValue(artifact.Json, out var original))
            { provenance.Add(new(artifact.Name, original)); continue; }
            originals.Add(artifact.Json, artifact.Name);
            provenance.Add(new(artifact.Name, null));
            unique.Add(artifact);
            var issue = compatibility.Include(artifact.Json, root);
            conflict ??= issue;
        }
        var result = conflict is not null && unique.Count > 1
            ? new CoverageEvidence
            {
                Limitation = "Coverage reports have incompatible or ambiguous overlapping identities. " + conflict + ". Inspect the individual collection scopes."
            }
            : ReadCoverage(unique.Select(x => x.Json).ToArray(), root);
        if (unique.Count > 1)
        {
            result.Scopes = unique.Select(x => new CoverageScopeEvidence(x.Name, ReadCoverage([x.Json], root))).ToList();
            if (result.Available)
            { result.Aggregation = "Combined " + unique.Count + " reports from this run using matching reported module/source/method/line/branch identities. Coverage is a union; line hits use the maximum reported count. Binary checksums are not provided by these reports."; }
        }
        result.Artifacts = provenance;
        if (unique.Count == 0)
        { result.Limitation = "No Coverlet JSON coverage was produced. Check the selected test projects' collector configuration."; }
        return result;
    }

    public CoverageEvidence ReadCoverage(IReadOnlyList<string> reports, string root)
    {
        var files = new Dictionary<string, (HashSet<int> All, HashSet<int> Covered)>(StringComparer.Ordinal);
        var branches = new Dictionary<string, bool>(StringComparer.Ordinal);
        var methods = new Dictionary<string, bool>(StringComparer.Ordinal);
        var hits = new Dictionary<(string Path, int Line), long>();
        var branchLocations = new Dictionary<string, (string Path, int Line)>(StringComparer.Ordinal);
        var methodFiles = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var json in reports)
        {
            using var document = JsonDocument.Parse(json);
            foreach (var module in document.RootElement.EnumerateObject())
            foreach (var source in module.Value.EnumerateObject())
            foreach (var type in source.Value.EnumerateObject())
            foreach (var method in type.Value.EnumerateObject())
            {
                var path = NormalizePath(source.Name, root);
                var scope = module.Name + "|" + path + "|" + type.Name + "|" + method.Name;
                if (!files.TryGetValue(path, out var lines)) { files[path] = lines = ([], []); }
                if (!method.Value.TryGetProperty("Lines", out var lineData)) { continue; }
                var reached = false;
                foreach (var line in lineData.EnumerateObject())
                {
                    if (!int.TryParse(line.Name, out var number) || number <= 0) { throw new InvalidDataException("Invalid coverage line identity."); }
                    lines.All.Add(number);
                    var count = line.Value.GetInt64();
                    hits[(path, number)] = Math.Max(hits.GetValueOrDefault((path, number)), count);
                    if (count > 0) { lines.Covered.Add(number); reached = true; }
                }
                if (lineData.EnumerateObject().Any()) { methods[scope] = methods.GetValueOrDefault(scope) || reached; methodFiles[scope] = path; }
                if (!method.Value.TryGetProperty("Branches", out var branchData)) { continue; }
                foreach (var branch in branchData.EnumerateArray())
                {
                    var key = scope + "|" + RequiredNumber(branch, "Line") + "|" + RequiredNumber(branch, "Offset") + "|" + RequiredNumber(branch, "EndOffset") + "|" + RequiredNumber(branch, "Path") + "|" + RequiredNumber(branch, "Ordinal");
                    branches[key] = branches.GetValueOrDefault(key) || RequiredNumber(branch, "Hits") > 0;
                    branchLocations[key] = (path, RequiredNumber(branch, "Line"));
                }
            }
        }
        return new()
        {
            Available = reports.Count > 0,
            Files = files.Select(x =>
            {
                var fileBranches = branches.Where(b => branchLocations[b.Key].Path == x.Key).ToArray();
                var fileMethods = methods.Where(m => methodFiles[m.Key] == x.Key).ToArray();
                var lineBranches = fileBranches.GroupBy(b => branchLocations[b.Key].Line).ToDictionary(g => g.Key, g => (Covered: g.Count(b => b.Value), Total: g.Count()));
                return new CoverageFileEvidence(x.Key, x.Value.All.Order().ToList(), x.Value.Covered.Order().ToList())
                {
                    Lines = x.Value.All.Order().Select(line => new CoverageLineEvidence(line, hits.GetValueOrDefault((x.Key, line)), lineBranches.GetValueOrDefault(line).Covered, lineBranches.GetValueOrDefault(line).Total)).ToList(),
                    BranchesCovered = fileBranches.Count(b => b.Value), BranchesTotal = fileBranches.Length,
                    MethodsCovered = fileMethods.Count(m => m.Value), MethodsTotal = fileMethods.Length
                };
            }).ToList(),
            BranchesTotal = branches.Count, BranchesCovered = branches.Count(x => x.Value),
            MethodsTotal = methods.Count, MethodsCovered = methods.Count(x => x.Value)
        };
    }

    public static string NormalizePath(string path, string root)
    {
        var native = path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        return (Path.IsPathRooted(native) ? Path.GetRelativePath(root, native) : native).Replace('\\', '/');
    }
    private static string Attribute(XElement? element, string name) => element?.Attribute(name)?.Value ?? "";
    private static string Value(XElement element, string name) => element.Descendants().FirstOrDefault(x => x.Name.LocalName == name)?.Value ?? "";
    private static string Text(JsonElement element, string name) => element.TryGetProperty(name, out var value) ? value.ToString() : "";
    private static int RequiredNumber(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number)
        ? number : throw new InvalidDataException("Coverage report is missing a numeric " + name + " value.");
    private static int Number(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : 0;
}
