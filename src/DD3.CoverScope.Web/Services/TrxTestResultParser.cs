using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DD3.CoverScope.Models;

namespace DD3.CoverScope.Services;

public sealed partial class TrxTestResultParser
{
    public TestRunSummary Parse(IEnumerable<string> reportPaths)
    {
        var passed = 0;
        var failed = 0;
        var skipped = 0;
        var total = 0;
        var duration = TimeSpan.Zero;
        var failures = new List<TestFailure>();

        foreach (var reportPath in reportPaths)
        {
            var document = XDocument.Load(reportPath, LoadOptions.None);
            var definitions = document
                .Descendants()
                .Where(x => x.Name.LocalName == "UnitTest")
                .Select(x => CreateDefinition(x))
                .Where(x => !string.IsNullOrWhiteSpace(x.Id))
                .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

            var results = document
                .Descendants()
                .Where(x => x.Name.LocalName == "UnitTestResult")
                .ToArray();

            if (results.Length == 0)
            {
                AddCounters(document, ref passed, ref failed, ref skipped, ref total);
                continue;
            }

            foreach (var result in results)
            {
                total++;
                var outcome = Attribute(result, "outcome");
                var resultDuration = ParseDuration(Attribute(result, "duration"));
                duration += resultDuration;

                if (IsPassed(outcome))
                {
                    passed++;
                    continue;
                }

                if (!IsFailure(outcome))
                {
                    skipped++;
                    continue;
                }

                failed++;
                var testId = Attribute(result, "testId");
                definitions.TryGetValue(testId, out var definition);
                failures.Add(CreateFailure(result, definition, resultDuration));
            }
        }

        return new TestRunSummary(passed, failed, skipped, total, duration, failures);
    }

    private static TestDefinition CreateDefinition(XElement element)
    {
        var method = element.Descendants().FirstOrDefault(x => x.Name.LocalName == "TestMethod");
        return new TestDefinition(
            Attribute(element, "id"),
            Attribute(method, "className"),
            Attribute(method, "name"),
            Attribute(method, "codeBase"));
    }

    private static TestFailure CreateFailure(XElement result, TestDefinition? definition, TimeSpan duration)
    {
        var displayName = Attribute(result, "testName");
        var fullyQualifiedName = JoinTestName(definition?.ClassName, definition?.MethodName, displayName);
        var errorInfo = result.Descendants().FirstOrDefault(x => x.Name.LocalName == "ErrorInfo");
        var message = ChildValue(errorInfo, "Message");
        var stackTrace = ChildValue(errorInfo, "StackTrace");
        var location = FindSourceLocation(stackTrace);

        return new TestFailure(
            string.IsNullOrWhiteSpace(displayName) ? fullyQualifiedName : displayName,
            fullyQualifiedName,
            FileName(definition?.CodeBase),
            duration,
            message,
            stackTrace,
            DescendantValue(result, "StdOut"),
            DescendantValue(result, "StdErr"),
            location.File,
            location.Line);
    }

    private static string JoinTestName(string? className, string? methodName, string fallback)
    {
        if (string.IsNullOrWhiteSpace(className)) return string.IsNullOrWhiteSpace(methodName) ? fallback : methodName;
        if (string.IsNullOrWhiteSpace(methodName)) return className;
        return $"{className}.{methodName}";
    }

    private static string FileName(string? path) =>
        Path.GetFileName((path ?? string.Empty).Replace('\\', '/')) ?? string.Empty;

    private static (string? File, int? Line) FindSourceLocation(string stackTrace)
    {
        foreach (Match match in SourceLocationRegex().Matches(stackTrace))
        {
            if (!int.TryParse(match.Groups["line"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var line))
                continue;

            var file = match.Groups["file"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(file)) return (file, line);
        }
        return (null, null);
    }

    private static void AddCounters(XDocument document, ref int passed, ref int failed, ref int skipped, ref int total)
    {
        var counters = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "Counters");
        if (counters is null) return;

        var filePassed = IntAttribute(counters, "passed");
        var fileFailed = IntAttribute(counters, "failed")
            + IntAttribute(counters, "error")
            + IntAttribute(counters, "timeout")
            + IntAttribute(counters, "aborted");
        var fileTotal = IntAttribute(counters, "total");
        passed += filePassed;
        failed += fileFailed;
        skipped += Math.Max(0, fileTotal - filePassed - fileFailed);
        total += fileTotal;
    }

    private static int IntAttribute(XElement element, string name) =>
        int.TryParse(Attribute(element, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static TimeSpan ParseDuration(string value) =>
        TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var duration) ? duration : TimeSpan.Zero;

    private static bool IsPassed(string outcome) =>
        string.Equals(outcome, "Passed", StringComparison.OrdinalIgnoreCase)
        || string.Equals(outcome, "Completed", StringComparison.OrdinalIgnoreCase);

    private static bool IsFailure(string outcome) =>
        string.Equals(outcome, "Failed", StringComparison.OrdinalIgnoreCase)
        || string.Equals(outcome, "Error", StringComparison.OrdinalIgnoreCase)
        || string.Equals(outcome, "Timeout", StringComparison.OrdinalIgnoreCase)
        || string.Equals(outcome, "Aborted", StringComparison.OrdinalIgnoreCase);

    private static string Attribute(XElement? element, string name) =>
        element?.Attributes().FirstOrDefault(x => x.Name.LocalName == name)?.Value ?? string.Empty;

    private static string ChildValue(XElement? element, string name) =>
        element?.Elements().FirstOrDefault(x => x.Name.LocalName == name)?.Value.Trim() ?? string.Empty;

    private static string DescendantValue(XElement element, string name) =>
        element.Descendants().FirstOrDefault(x => x.Name.LocalName == name)?.Value.Trim() ?? string.Empty;

    [GeneratedRegex(@"\sin\s(?<file>.+):line\s(?<line>\d+)\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex SourceLocationRegex();

    private sealed record TestDefinition(string Id, string ClassName, string MethodName, string CodeBase);
}
