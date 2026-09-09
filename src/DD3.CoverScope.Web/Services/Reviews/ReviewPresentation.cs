using DD3.CoverScope.Models.Reviews;

namespace DD3.CoverScope.Services.Reviews;

// Projections of saved evidence only. Never reads the current working tree.
public static class ReviewPresentation
{
    public static CoverageEvidence FilterCoverage(ReviewEvidence evidence, CoverageEvidence coverage, string range)
    {
        if (range == "All code" || !coverage.Available) { return coverage; }
        if (!evidence.SourceMatches)
        { return new() { Limitation = "Branch coverage is unavailable because source consistency was not established." }; }

        var changes = evidence.Files.Where(x => x.Kind != ChangeKind.Deleted).ToDictionary(x => x.Path, StringComparer.Ordinal);
        var files = new List<CoverageFileEvidence>();
        foreach (var file in coverage.Files.Where(x => changes.ContainsKey(x.Path)))
        {
            if (range == "Changed files") { files.Add(file); continue; }
            var changed = changes[file.Path].AddedLines.ToHashSet();
            var executable = file.ExecutableLines.Where(changed.Contains).ToList();
            if (executable.Count == 0) { continue; }
            var lines = file.Lines?.Where(x => changed.Contains(x.Number)).ToList();
            files.Add(new(file.Path, executable, file.CoveredLines.Where(changed.Contains).ToList())
            {
                Source = file.Source,
                Lines = lines,
                BranchesCovered = lines?.Sum(x => x.BranchesCovered),
                BranchesTotal = lines?.Sum(x => x.BranchesTotal)
            });
        }

        return new()
        {
            Available = true,
            Limitation = coverage.Limitation,
            Aggregation = coverage.Aggregation,
            Files = files,
            BranchesCovered = files.All(x => x.BranchesCovered is not null) ? files.Sum(x => x.BranchesCovered) : null,
            BranchesTotal = files.All(x => x.BranchesTotal is not null) ? files.Sum(x => x.BranchesTotal) : null,
            MethodsCovered = range == "Changed files" && files.All(x => x.MethodsCovered is not null) ? files.Sum(x => x.MethodsCovered) : null,
            MethodsTotal = range == "Changed files" && files.All(x => x.MethodsTotal is not null) ? files.Sum(x => x.MethodsTotal) : null
        };
    }

    public static string Metric(int? covered, int? total) => covered is null || total is null ? "Unavailable"
        : total == 0 ? "Not applicable" : FormattableString.Invariant($"{covered}/{total} ({100d * covered / total:0.0}%)");

    public static string Scope(string path) => path.Split('/').Any(x => x is "obj" or "bin") ? "Generated / build output"
        : path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase) ? "Razor"
        : path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? "C#" : "Other sources";

    public static string Project(ReviewEvidence evidence, string path) =>
        evidence.Code.Documents.FirstOrDefault(x => x.Path == path)?.Project
        ?? evidence.Code.Types.Select(x => x.Project).Distinct().OrderByDescending(x => x.Length)
            .FirstOrDefault(x => path.StartsWith(x[..(x.LastIndexOf('/') + 1)], StringComparison.Ordinal)) ?? "Other files";

    public static string Namespace(ReviewEvidence evidence, TypeEvidence type)
    {
        if (type.Namespace is not null) { return type.Namespace; }
        var outer = evidence.Code.Types.Where(x => x.Project == type.Project && type.FullName.StartsWith(x.FullName + ".", StringComparison.Ordinal))
            .OrderBy(x => x.FullName.Length).FirstOrDefault() ?? type;
        var marker = outer.FullName.LastIndexOf("." + outer.Name, StringComparison.Ordinal);
        return marker < 0 ? "Global namespace" : outer.FullName[..marker];
    }

    public static string? Source(ReviewEvidence evidence, string path, string revision, CoverageEvidence? coverage = null)
    {
        var change = evidence.Files.FirstOrDefault(x => x.Path == path || (revision == "Baseline" && x.OldPath == path));
        if (change is not null) { return revision == "Baseline" ? change.Before : change.After; }
        return evidence.Code.Documents.FirstOrDefault(x => x.Path == path && x.Revision == revision)?.Text
            ?? (revision == "Head" ? coverage?.Files.FirstOrDefault(x => x.Path == path)?.Source : null);
    }
}
