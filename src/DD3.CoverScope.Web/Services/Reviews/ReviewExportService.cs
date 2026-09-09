using System.Text;
using System.Text.Json;
using DD3.CoverScope.Models.Reviews;

namespace DD3.CoverScope.Services.Reviews;

public sealed class ReviewExportService
{
    public string Json(ReviewSnapshot snapshot) => JsonSerializer.Serialize(ExportModel(snapshot), ReviewStore.Json);

    public string Markdown(ReviewSnapshot snapshot)
    {
        var record = snapshot.Record;
        var text = new StringBuilder("# CoverScope review\n\n");
        text.AppendLine($"Run: {Inline(record.Id)}  \nStatus: {record.Status}  \nStarted: {record.StartedAt:O}  \nEnded: {record.EndedAt:O}\n");
        text.AppendLine($"Target path: {Inline(record.TargetPath)}\n");
        if (record.Status == ReviewStatus.Cancelled)
        { text.AppendLine("Analysis was discarded. Only the run record remains."); return text.ToString(); }
        if (record.OperationalError is { Length: > 0 } error)
        { text.AppendLine("Operational error:"); AppendBlock(text, error); }
        if (record.Status is not (ReviewStatus.Finished or ReviewStatus.Failed or ReviewStatus.Interrupted) || snapshot.Evidence is not { } evidence)
        { text.AppendLine("Analysis is unavailable for this attempt."); return text.ToString(); }
        if (record.Status == ReviewStatus.Finished)
        { text.AppendLine("Finished means collection ended; it does not mean all checks passed or the changes are acceptable.\n"); }
        text.AppendLine($"Commit: {Inline(evidence.Comparison.Commit)}  \nTarget: {Inline(evidence.Comparison.TargetRef)} ({Inline(evidence.Comparison.TargetCommit)})  \nCommon ancestor: {Inline(evidence.Comparison.Baseline)}\n");
        text.AppendLine($"Configuration: {Inline(evidence.Settings.Configuration)}  \nSource matches reviewed commit: {evidence.SourceMatches}  \nSource consistency: {Inline(evidence.SourceConsistency)}\n");
        AppendSummary(text, evidence);
        text.AppendLine("\n## Scope and limitations\n");
        text.AppendLine(Inline(evidence.Code.Scope) + ".\n");
        foreach (var limitation in evidence.Code.Limitations) { text.AppendLine("- " + Inline(limitation)); }
        foreach (var diagnostic in evidence.Code.Diagnostics.GroupBy(x => (x.Category, x.Kind, x.AffectsCompleteness)))
        { text.AppendLine($"- Index diagnostics: {diagnostic.Count()} {Inline(diagnostic.Key.Category)} messages; original kind {Inline(diagnostic.Key.Kind)}; affects completeness: {diagnostic.Key.AffectsCompleteness}. Full messages are preserved in JSON."); }
        if (evidence.Coverage.Limitation is { Length: > 0 } coverageLimitation)
        { text.AppendLine("- Coverage: " + Inline(coverageLimitation)); }
        text.AppendLine("\n## Verification\n");
        foreach (var check in new[] { evidence.Build, evidence.Tests, evidence.Formatting })
        { text.AppendLine($"- {Inline(check.Name)}: {check.Status}. {Inline(check.Message)}"); }
        foreach (var scope in evidence.Coverage.Scopes)
        { AppendCoverage(text, "Coverage scope " + scope.Name, scope.Evidence); }
        if (evidence.Coverage.Aggregation is { } aggregation) { text.AppendLine("\n" + Inline(aggregation)); }
        if (evidence.Coverage.Available)
        {
            text.AppendLine("\nCoverage source breakdown:\n");
            foreach (var group in evidence.Coverage.Files.GroupBy(x => ReviewPresentation.Scope(x.Path)))
            { text.AppendLine($"- {Inline(group.Key)}: {group.Count()} files; lines {Metric(group.Sum(x => x.CoveredLines.Count), group.Sum(x => x.ExecutableLines.Count))}."); }
            text.AppendLine("\nSource scopes are based on reported paths; bin/obj output is separated. Declaration counts cover indexed C# types only.\n");
        }
        foreach (var artifact in evidence.Coverage.Artifacts.Where(x => x.DuplicateOf is not null))
        { text.AppendLine($"- Duplicate coverage attachment: {Inline(artifact.Name)} (same contents as {Inline(artifact.DuplicateOf)}; counted once)."); }
        text.AppendLine("\n## Changed files\n");
        foreach (var file in evidence.Files)
        {
            var lines = file.Added is not null && file.Removed is not null ? $"+{file.Added} / -{file.Removed}" : "text line counts unavailable";
            var rename = file.OldPath is null ? "" : $" (from {Inline(file.OldPath)})";
            text.AppendLine($"- {file.Kind}: {Inline(file.Path)}{rename}; {lines}");
        }
        foreach (var execution in evidence.Executions.Where(x => x.Outcome is not ("Passed" or "Completed")))
        {
            text.AppendLine($"\n## Test: {Inline(execution.Name)} — {Inline(execution.Outcome)}\n");
            if (!string.IsNullOrWhiteSpace(execution.Message)) { AppendBlock(text, execution.Message); }
            if (!string.IsNullOrWhiteSpace(execution.StackTrace)) { AppendBlock(text, execution.StackTrace); }
        }
        if (evidence.FormattingFindings.Count > 0)
        {
            text.AppendLine("\n## Formatting findings\n");
            var changedPaths = evidence.Files.Where(x => x.Kind != ChangeKind.Deleted).Select(x => x.Path).ToHashSet(StringComparer.Ordinal);
            foreach (var rule in evidence.FormattingFindings.GroupBy(x => x.Id).OrderBy(x => x.Key, StringComparer.Ordinal))
            { text.AppendLine($"- {Inline(rule.Key)}: {rule.Count()} findings in {rule.Select(x => x.Path).Distinct().Count()} files; {rule.Count(x => changedPaths.Contains(x.Path))} findings in changed files."); }
            text.AppendLine("\nLine-ending findings are grouped by file below; individual locations remain in JSON.\n");
            foreach (var file in evidence.FormattingFindings.Where(x => x.Id == "ENDOFLINE").GroupBy(x => x.Path).OrderBy(x => x.Key, StringComparer.Ordinal))
            { text.AppendLine($"- {Inline(file.Key)} — ENDOFLINE: {file.Count()} locations. {Inline(file.First().Message)}"); }
            foreach (var finding in evidence.FormattingFindings.Where(x => x.Id != "ENDOFLINE"))
            { text.AppendLine($"- {Inline(finding.Path)}:{finding.Line}:{finding.Column} — {Inline(finding.Id)}: {Inline(finding.Message)}"); }
        }
        text.AppendLine("\nFull source, diffs, relationships, tool output and execution details are available in the JSON evidence export.");
        return text.ToString();
    }

    private static void AppendSummary(StringBuilder text, ReviewEvidence evidence)
    {
        text.AppendLine("## Summary\n");
        text.AppendLine($"- Changes: {evidence.Files.Count} files; +{evidence.Files.Sum(x => x.Added ?? 0)} / -{evidence.Files.Sum(x => x.Removed ?? 0)} text lines.");
        if (evidence.Code.Complete)
        {
            var members = evidence.Code.Types.SelectMany(x => x.Members).Where(x => x.Change != "Unchanged").ToArray();
            text.AppendLine($"- Code comparison: complete within the recorded scope; {evidence.Code.Types.Count(x => x.Change != "Unchanged")} changed types; {members.Length} changed members; {members.Count(x => x.IsTest)} changed xUnit definitions (not test executions).");
        }
        else { text.AppendLine("- Code comparison: partial or unavailable. Changed type, member and test-definition counts are unavailable."); }
        var outcomes = string.Join(", ", evidence.Executions.GroupBy(x => x.Outcome).OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => $"{x.Count()} {Inline(x.Key)}"));
        text.AppendLine(evidence.Executions.Count > 0
            ? $"- Tests: {evidence.Executions.Count} recorded executions — {outcomes}."
            : $"- Tests: {(evidence.Tests.Status == CheckStatus.NotRun ? "not run" : "execution results unavailable")}.");
        AppendCoverage(text, "Coverage", evidence.Coverage);
        text.AppendLine(evidence.Formatting.Status == CheckStatus.Completed || evidence.FormattingFindings.Count > 0
            ? $"- Formatting: {evidence.FormattingFindings.Count} recorded findings; {evidence.Formatting.Status}."
            : $"- Formatting: {evidence.Formatting.Status}; finding count unavailable.");
    }

    private static void AppendCoverage(StringBuilder text, string name, CoverageEvidence coverage)
    {
        if (!coverage.Available) { text.AppendLine($"- {Inline(name)}: unavailable. {Inline(coverage.Limitation)}"); return; }
        text.AppendLine($"- {Inline(name)}: lines {Metric(coverage.Files.Sum(x => x.CoveredLines.Count), coverage.Files.Sum(x => x.ExecutableLines.Count))}; " +
            $"branches {Metric(coverage.BranchesCovered, coverage.BranchesTotal)}; methods {Metric(coverage.MethodsCovered, coverage.MethodsTotal)}.");
    }

    private static string Metric(int? covered, int? total) => covered is null || total is null ? "unavailable"
        : total == 0 ? "not applicable (0 measured)" : FormattableString.Invariant($"{covered}/{total} ({100d * covered / total:0.0}%)");

    private static string Inline(string? value)
    {
        var text = new StringBuilder();
        foreach (var ch in value ?? "")
        {
            if (ch is '\r' or '\n') { text.Append(' '); }
            else if (ch == '<') { text.Append("&lt;"); }
            else if (ch == '>') { text.Append("&gt;"); }
            else if (ch == '&') { text.Append("&amp;"); }
            else { if ("\\`*_{}[]()#+-.!|".Contains(ch)) { text.Append('\\'); } text.Append(ch); }
        }
        return text.ToString();
    }

    private static void AppendBlock(StringBuilder text, string value)
    {
        var longest = 0;
        var current = 0;
        foreach (var ch in value) { current = ch == '`' ? current + 1 : 0; longest = Math.Max(longest, current); }
        var fence = new string('`', Math.Max(3, longest + 1));
        text.AppendLine(fence + "text");
        text.AppendLine(value);
        text.AppendLine(fence + "\n");
    }

    private static object ExportModel(ReviewSnapshot snapshot) => snapshot.Record.Status switch
    {
        ReviewStatus.Cancelled => snapshot.Record with { OperationalError = null },
        ReviewStatus.Cancelling or ReviewStatus.CancellationIncomplete or ReviewStatus.Preparing or ReviewStatus.Running or ReviewStatus.Finalising => snapshot.Record,
        _ => snapshot
    };
}
