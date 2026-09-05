namespace DD3.CoverScope.Models;

public sealed record CoverageMetric(int Covered, int Total)
{
    public double Percent => Total == 0 ? 0 : (double)Covered / Total * 100;
    public string Display => Total == 0 ? "—" : $"{Percent:0.0}%";
    public bool HasGaps => Total > 0 && Covered < Total;
}

public sealed record CoverageLine(int Number, int Hits, int? BranchesCovered = null, int? BranchesTotal = null)
{
    public bool IsCovered => Hits > 0;
    public bool IsBranch => BranchesTotal > 0;
    public bool IsPartial => IsBranch && BranchesCovered < BranchesTotal;
}

public sealed record MethodCoverage(
    string Name,
    string Signature,
    int StartLine,
    IReadOnlyList<CoverageLine> SourceLines)
{
    public CoverageMetric Lines => new(SourceLines.Count(x => x.IsCovered), SourceLines.Count);
    public CoverageMetric Branches => new(
        SourceLines.Sum(x => x.BranchesCovered ?? 0),
        SourceLines.Sum(x => x.BranchesTotal ?? 0));
}

public sealed record ClassCoverage(
    string Name,
    string FullName,
    string Namespace,
    string RelativePath,
    string? ResolvedPath,
    IReadOnlyList<CoverageLine> Lines,
    IReadOnlyList<MethodCoverage> Methods)
{
    public CoverageMetric LineMetric => new(Lines.Count(x => x.IsCovered), Lines.Count);
    public CoverageMetric BranchMetric => new(
        Lines.Sum(x => x.BranchesCovered ?? 0),
        Lines.Sum(x => x.BranchesTotal ?? 0));
}

public sealed record FileCoverage(
    string RelativePath,
    string? ResolvedPath,
    IReadOnlyList<CoverageLine> Lines,
    IReadOnlyList<ClassCoverage> Classes)
{
    public string DisplayName => Path.GetFileName(RelativePath);
    public CoverageMetric LineMetric => new(Lines.Count(x => x.IsCovered), Lines.Count);
    public CoverageMetric BranchMetric => new(
        Lines.Sum(x => x.BranchesCovered ?? 0),
        Lines.Sum(x => x.BranchesTotal ?? 0));
}

public sealed record NamespaceCoverage(string Name, IReadOnlyList<ClassCoverage> Classes)
{
    public CoverageMetric LineMetric => CoverageMath.Combine(Classes.Select(x => x.LineMetric));
    public CoverageMetric BranchMetric => CoverageMath.Combine(Classes.Select(x => x.BranchMetric));
}

public sealed record PackageCoverage(
    string Name,
    IReadOnlyList<FileCoverage> Files,
    IReadOnlyList<NamespaceCoverage> Namespaces)
{
    public CoverageMetric LineMetric => CoverageMath.Combine(Files.Select(x => x.LineMetric));
    public CoverageMetric BranchMetric => CoverageMath.Combine(Files.Select(x => x.BranchMetric));
}

public sealed record CoverageReport(
    string Name,
    DateTimeOffset GeneratedAt,
    string ReportPath,
    IReadOnlyList<string> SourceRoots,
    IReadOnlyList<PackageCoverage> Packages)
{
    public IReadOnlyList<FileCoverage> Files => Packages.SelectMany(x => x.Files).ToArray();
    public CoverageMetric Lines => CoverageMath.Combine(Files.Select(x => x.LineMetric));
    public CoverageMetric Branches => CoverageMath.Combine(Files.Select(x => x.BranchMetric));
    public CoverageMetric Methods => CoverageMath.Combine(
        Packages.SelectMany(x => x.Namespaces)
            .SelectMany(x => x.Classes)
            .SelectMany(x => x.Methods)
            .Select(x => x.Lines.Total > 0
                ? new CoverageMetric(x.Lines.Covered > 0 ? 1 : 0, 1)
                : new CoverageMetric(0, 0)));
}

public static class CoverageMath
{
    public static CoverageMetric Combine(IEnumerable<CoverageMetric> metrics)
    {
        var covered = 0;
        var total = 0;
        foreach (var metric in metrics)
        {
            covered += metric.Covered;
            total += metric.Total;
        }
        return new CoverageMetric(covered, total);
    }
}

public sealed record SourceLine(int Number, string Text, CoverageLine? Coverage)
{
    public string State => Coverage switch
    {
        null => "neutral",
        { IsPartial: true } => "partial",
        { IsCovered: true } => "covered",
        _ => "uncovered"
    };
}

public sealed class CoverageSettings
{
    public string Exclude { get; set; } = string.Empty;
    public string ExcludeByFile { get; set; } = string.Empty;
    public string ExcludeByAttribute { get; set; } = string.Empty;
    public bool SkipAutoProps { get; set; }
}

public enum CoverageRunOutcome
{
    Succeeded,
    TestsFailed,
    ExecutionFailed,
    Cancelled
}

public sealed record TestFailure(
    string Name,
    string FullyQualifiedName,
    string Assembly,
    TimeSpan Duration,
    string Message,
    string StackTrace,
    string StandardOutput,
    string StandardError,
    string? SourceFile,
    int? SourceLine);

public sealed record TestRunSummary(
    int Passed,
    int Failed,
    int Skipped,
    int Total,
    TimeSpan Duration,
    IReadOnlyList<TestFailure> Failures);

public sealed record CoverageRunResult(
    CoverageRunOutcome Outcome,
    string Message,
    string Output,
    string? ReportPath = null,
    TestRunSummary? Tests = null,
    CoverageRunManifest? Run = null)
{
    public bool Success => Outcome == CoverageRunOutcome.Succeeded;
    public bool HasCoverage => ReportPath is not null;
}

public enum CoverageMetricScope
{
    Project,
    Namespace,
    Class
}

public sealed record CoverageMetricRow(
    CoverageMetricScope Scope,
    string Name,
    string FullName,
    CoverageMetric Lines,
    CoverageMetric Branches,
    CoverageMetric Methods,
    int FileCount,
    int ChildCount,
    PackageCoverage Project,
    NamespaceCoverage? Namespace,
    IReadOnlyList<ClassCoverage> ClassFragments);
