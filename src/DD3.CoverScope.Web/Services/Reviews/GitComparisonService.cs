using System.Text.RegularExpressions;
using DD3.CoverScope.Brokers;
using DD3.CoverScope.Models.Reviews;

namespace DD3.CoverScope.Services.Reviews;

public sealed partial class GitComparisonService(IProcessBroker processes, IFileSystemBroker files)
{
    public async Task<RepositoryContext> InspectAsync(string target, CancellationToken token = default)
    {
        var full = Path.GetFullPath(target);
        if (!files.FileExists(full) || !SolutionFileBrowser.IsSupportedFile(full)) { throw new ArgumentException("Select an existing solution or project."); }
        var directory = Path.GetDirectoryName(full)!;
        var root = (await GitAsync(directory, ["rev-parse", "--show-toplevel"], token)).Trim();
        var gitDirectory = (await GitAsync(root, ["rev-parse", "--absolute-git-dir"], token)).Trim();
        var commit = (await GitAsync(root, ["rev-parse", "--verify", "HEAD^{commit}"], token)).Trim();
        var branch = (await GitAsync(root, ["rev-parse", "--abbrev-ref", "HEAD"], token)).Trim();
        var branches = (await GitAsync(root, ["for-each-ref", "--format=%(refname)", "refs/heads/", "refs/remotes/"], token))
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !x.EndsWith("/HEAD", StringComparison.Ordinal)).ToArray();
        return new(root, gitDirectory, commit, branch == "HEAD" ? null : branch, branches);
    }

    public async Task AssertCleanAsync(string root, CancellationToken token)
    {
        var status = await StatusAsync(root, token);
        if (status.Length > 0) { throw new InvalidOperationException("Commit or otherwise resolve pending changes before starting a run. Git status: " + status.Replace('\0', '\n')); }
    }

    public Task<string> StatusAsync(string root, CancellationToken token) =>
        GitAsync(root, ["status", "--porcelain=v1", "-z", "--untracked-files=all", "--ignore-submodules=none"], token);

    public async Task<ComparisonContext> ResolveAsync(RepositoryContext repository, ReviewRequest request, CancellationToken token)
    {
        if (!repository.Branches.Contains(request.Settings.TargetBranch, StringComparer.Ordinal))
        { throw new ArgumentException("Select an available local branch reference. Fetch missing references through your Git client."); }
        var target = (await GitAsync(repository.Root, ["rev-parse", "--verify", request.Settings.TargetBranch + "^{commit}"], token)).Trim();
        var bases = (await GitAsync(repository.Root, ["merge-base", "--all", repository.Commit, target], token)).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (bases.Length != 1) { throw new InvalidOperationException("This comparison requires exactly one available common ancestor. Resolve missing or ambiguous history in Git."); }
        return new(repository.Root, Path.GetFullPath(request.TargetPath), repository.Branch, repository.Commit, request.Settings.TargetBranch, target, bases[0].Trim());
    }

    public async Task<List<FileChange>> ChangesAsync(ComparisonContext comparison, CancellationToken token)
    {
        string[] common = ["diff", "--no-ext-diff", "--no-textconv", "--find-renames=50%", comparison.Baseline, comparison.Commit];
        var names = (await GitAsync(comparison.Repository, [.. common, "--name-status", "-z", "--"], token)).Split('\0');
        var stats = ParseNumstat(await GitAsync(comparison.Repository, [.. common, "--numstat", "-z", "--"], token));
        var gitlinks = new HashSet<string>(StringComparer.Ordinal);
        foreach (var revision in new[] { comparison.Baseline, comparison.Commit })
        {
            var entries = (await GitAsync(comparison.Repository, ["ls-tree", "-r", "-z", revision], token)).Split('\0', StringSplitOptions.RemoveEmptyEntries);
            foreach (var entry in entries.Where(x => x.StartsWith("160000 ", StringComparison.Ordinal)))
            { gitlinks.Add(entry[(entry.IndexOf('\t') + 1)..]); }
        }
        var changes = new List<FileChange>();
        for (var i = 0; i < names.Length && names[i].Length > 0;)
        {
            token.ThrowIfCancellationRequested();
            var status = names[i++];
            var first = names[i++];
            var rename = status.StartsWith('R');
            var path = rename ? names[i++] : first;
            var kind = status[0] switch { 'A' => ChangeKind.Added, 'D' => ChangeKind.Deleted, 'R' => ChangeKind.Renamed, 'T' => ChangeKind.TypeChanged, _ => ChangeKind.Modified };
            stats.TryGetValue(path, out var counts);
            if (gitlinks.Contains(path) || gitlinks.Contains(first)) { counts = (null, null); }
            var change = new FileChange(path, rename ? first : null, kind, counts.Added, counts.Removed);
            string[] paths = rename ? [first, path] : [path];
            change.Diff = await GitAsync(comparison.Repository, [.. common, "--unified=3", "--", .. paths], token);
            change.AddedLines = AddedLines(change.Diff);
            if (counts.Added is not null)
            {
                if (kind != ChangeKind.Added) { change.Before = await ReadSourceAsync(comparison.Repository, comparison.Baseline, first, token); }
                if (kind != ChangeKind.Deleted) { change.After = await ReadSourceAsync(comparison.Repository, comparison.Commit, path, token); }
            }
            changes.Add(change);
        }
        return changes;
    }

    public Task<string> ReadSourceAsync(string root, string commit, string path, CancellationToken token) =>
        GitAsync(root, ["show", commit + ":" + path], token);

    public async Task CaptureCoverageSourcesAsync(ReviewEvidence evidence, CancellationToken token)
    {
        var comparison = evidence.Comparison;
        var coverageFiles = evidence.Coverage.Files.Concat(evidence.Coverage.Scopes.SelectMany(x => x.Evidence.Files)).ToArray();
        if (coverageFiles.Length == 0) { return; }
        var entries = (await GitAsync(comparison.Repository, ["ls-tree", "-r", "-z", comparison.Commit], token)).Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var tracked = entries.Where(x => x.StartsWith("100644 ", StringComparison.Ordinal) || x.StartsWith("100755 ", StringComparison.Ordinal))
            .Select(x => x[(x.IndexOf('\t') + 1)..]).ToHashSet(StringComparer.Ordinal);
        foreach (var group in coverageFiles.Where(x => tracked.Contains(x.Path)).GroupBy(x => x.Path, StringComparer.Ordinal))
        {
            var source = evidence.Files.FirstOrDefault(x => x.Path == group.Key)?.After
                ?? await ReadSourceAsync(comparison.Repository, comparison.Commit, group.Key, token);
            foreach (var file in group) { file.Source = source; }
        }
    }

    public async Task<string[]> TrackedPathsAsync(string root, string commit, CancellationToken token) =>
        (await GitAsync(root, ["ls-tree", "-r", "--name-only", "-z", commit, "--"], token)).Split('\0', StringSplitOptions.RemoveEmptyEntries);

    public Task<string> VersionAsync(string root, CancellationToken token) => GitAsync(root, ["--version"], token);

    private async Task<string> GitAsync(string directory, string[] arguments, CancellationToken token)
    {
        var result = await processes.ExecuteAsync(new("git", ["--literal-pathspecs", .. arguments], directory), token);
        if (result.ExitCode != 0) { throw new InvalidOperationException("Git could not complete the operation: " + result.Error.Trim()); }
        if (result.Output.EndsWith("[Output truncated after 2,000,000 characters]" + Environment.NewLine, StringComparison.Ordinal))
        { throw new InvalidDataException("Git output exceeded the supported size. This comparison is unavailable rather than silently truncated."); }
        return result.Output;
    }

    internal static Dictionary<string, (int? Added, int? Removed)> ParseNumstat(string output)
    {
        var result = new Dictionary<string, (int?, int?)>(StringComparer.Ordinal);
        var fields = output.Split('\0');
        for (var i = 0; i < fields.Length && fields[i].Length > 0; i++)
        {
            var row = fields[i].Split('\t', 3);
            if (row.Length != 3) { throw new InvalidDataException("Invalid Git numstat record."); }
            var path = row[2];
            if (path.Length == 0) { i++; path = fields[++i]; }
            result[path] = (int.TryParse(row[0], out var a) ? a : null, int.TryParse(row[1], out var r) ? r : null);
        }
        return result;
    }

    internal static List<int> AddedLines(string patch)
    {
        var result = new List<int>();
        var lineNumber = 0;
        var inHunk = false;
        foreach (var line in patch.Split('\n'))
        {
            var match = Hunk().Match(line);
            if (match.Success) { lineNumber = int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture); inHunk = true; continue; }
            if (!inHunk) { continue; }
            if (line.StartsWith('+')) { result.Add(lineNumber++); }
            else if (line.StartsWith(' ')) { lineNumber++; }
        }
        return result;
    }

    [GeneratedRegex(@"^@@ -\d+(?:,\d+)? \+(\d+)(?:,\d+)? @@")]
    private static partial Regex Hunk();
}
