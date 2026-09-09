using System.Text.Json;
using DD3.CoverScope.Brokers;
using DD3.CoverScope.Models.Reviews;

namespace DD3.CoverScope.Services.Reviews;

public sealed class CodeComparisonService(IProcessBroker processes, IFileSystemBroker files)
{
    public async Task<CodeEvidence> AnalyseAsync(ComparisonContext comparison, ReviewSettings settings, string directory, Action<string> progress, CancellationToken token)
    {
        try
        {
            files.CreateDirectory(directory);
            var relativeTarget = Path.GetRelativePath(comparison.Repository, comparison.TargetPath);
            if (!settings.Tests)
            {
                progress("Restoring reviewed source references for indexing");
                var restoreHead = await processes.ExecuteAsync(new("dotnet", ["restore", comparison.TargetPath, "--disable-build-servers"], Path.GetDirectoryName(comparison.TargetPath)!), token);
                if (restoreHead.ExitCode != 0) { throw new InvalidOperationException("Reviewed source restore failed: " + restoreHead.Error); }
            }
            progress("Indexing reviewed source");
            var head = await IndexAsync(comparison.Repository, comparison.TargetPath, settings.Configuration, Path.Combine(directory, "head-index.json"), token);
            progress("Preparing baseline source");
            var baselineRoot = Path.Combine(directory, "baseline-source");
            files.CreateDirectory(baselineRoot);
            var archive = Path.Combine(directory, "baseline.tar");
            var extract = await processes.ExecuteAsync(new("git", ["archive", "--format=tar", "--output=" + archive, comparison.Baseline], comparison.Repository), token);
            if (extract.ExitCode != 0) { throw new InvalidOperationException("Could not materialise baseline source: " + extract.Error); }
            await files.ExtractTarAsync(archive, baselineRoot, token);
            var baselineTarget = Path.Combine(baselineRoot, relativeTarget);
            if (!files.FileExists(baselineTarget))
            { MarkUnknown(head); head.Limitations.Add("Selected target did not exist at the baseline; code change counts are unavailable."); return head; }
            progress("Restoring baseline references for code comparison");
            var restore = await processes.ExecuteAsync(new("dotnet", ["restore", baselineTarget, "--disable-build-servers", "-p:UseSharedCompilation=false"], Path.GetDirectoryName(baselineTarget)!), token);
            if (restore.ExitCode != 0)
            { MarkUnknown(head); head.Limitations.Add("Baseline restore failed; change counts are unavailable. " + restore.Error); return head; }
            progress("Indexing baseline source");
            var baseline = await IndexAsync(baselineRoot, baselineTarget, settings.Configuration, Path.Combine(directory, "baseline-index.json"), token);
            return Compare(baseline, head);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not ProcessStoppingException)
        { return new() { Complete = false, Limitations = ["Code comparison unavailable: " + ex.Message] }; }
    }

    private static void MarkUnknown(CodeEvidence evidence)
    {
        evidence.Complete = false;
        foreach (var type in evidence.Types)
        {
            type.Change = "Unknown";
            foreach (var member in type.Members) { member.Change = "Unknown"; }
        }
    }

    private async Task<CodeEvidence> IndexAsync(string root, string target, string configuration, string output, CancellationToken token)
    {
        var assembly = typeof(CodeComparisonService).Assembly.Location;
        var result = await processes.ExecuteAsync(new("dotnet", [assembly, "--internal-index", root, target, output, configuration], Path.GetDirectoryName(target)!), token);
        if (result.ExitCode != 0) { throw new InvalidOperationException("Source index failed: " + result.Error); }
        return JsonSerializer.Deserialize<CodeEvidence>(await files.ReadAsync(output, token), ReviewStore.Json)
            ?? throw new InvalidDataException("Missing source index.");
    }

    internal static CodeEvidence Compare(CodeEvidence before, CodeEvidence after)
    {
        var previous = before.Types.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var types = new List<TypeEvidence>();
        foreach (var current in after.Types)
        {
            if (!previous.Remove(current.Id, out var old))
            {
                current.Change = "Added";
                foreach (var member in current.Members) { member.Change = "Added"; }
            }
            else
            {
                var oldMembers = old.Members.ToDictionary(x => x.Id, StringComparer.Ordinal);
                foreach (var member in current.Members)
                {
                    if (!oldMembers.Remove(member.Id, out var oldMember)) { member.Change = "Added"; continue; }
                    member.Before = oldMember.Text;
                    member.BeforePath = oldMember.Path;
                    member.BeforeLine = oldMember.Line;
                    member.Change = member.Fingerprint == oldMember.Fingerprint ? "Unchanged" : "Modified";
                }
                foreach (var member in oldMembers.Values) { member.Change = "Deleted"; member.Before = member.Text; current.Members.Add(member); }
                current.Change = current.DeclarationFingerprint != old.DeclarationFingerprint || current.Members.Any(x => x.Change != "Unchanged") ? "Modified" : "Unchanged";
            }
            types.Add(current);
        }
        foreach (var old in previous.Values)
        {
            old.Change = "Deleted";
            foreach (var member in old.Members) { member.Change = "Deleted"; member.Before = member.Text; }
            types.Add(old);
        }
        return after with
        {
            Documents = after.Documents.Concat(before.Documents.Select(x => x with { Revision = "Baseline" })).DistinctBy(x => (x.Project, x.Path, x.Revision)).ToList(),
            Complete = before.Complete && after.Complete,
            Types = types.OrderBy(x => x.FullName, StringComparer.Ordinal).ToList(),
            Limitations = before.Limitations.Concat(after.Limitations).Distinct().ToList(),
            Diagnostics = after.Diagnostics.Concat(before.Diagnostics.Select(x => x with { Revision = "Baseline" })).Distinct().ToList(),
            Relationships = after.Relationships.Concat(before.Relationships.Select(x => x with { Revision = "Baseline" })).Distinct().ToList()
        };
    }
}
