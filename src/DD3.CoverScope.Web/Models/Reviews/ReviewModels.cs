using System.Text.Json.Serialization;

namespace DD3.CoverScope.Models.Reviews;

public enum ReviewStatus { Preparing, Running, Finalising, Finished, Failed, Cancelling, Cancelled, Interrupted, CancellationIncomplete }
public enum CheckStatus { NotRun, Running, Completed, FailedToExecute, Blocked }
public enum ChangeKind { Added, Modified, Deleted, Renamed, TypeChanged }

public sealed record ReviewSettings(string TargetBranch = "", bool Tests = true, bool Formatting = true, string Configuration = "Debug");
public sealed record ReviewRequest(string TargetPath, ReviewSettings Settings);
public sealed record RepositoryContext(string Root, string GitDirectory, string Commit, string? Branch, IReadOnlyList<string> Branches);
public sealed record ComparisonContext(string Repository, string TargetPath, string? Branch, string Commit, string TargetRef, string TargetCommit, string Baseline);
public sealed record ReviewRecord(string Id, string Repository, string TargetPath, string TargetRef, DateTimeOffset StartedAt)
{
    public int SchemaVersion { get; init; } = 2;
    public string? Commit { get; init; }
    public string? TargetCommit { get; init; }
    public string? Baseline { get; init; }
    public DateTimeOffset? EndedAt { get; init; }
    public ReviewStatus Status { get; init; } = ReviewStatus.Preparing;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OperationalError { get; init; }
}

public sealed record CheckEvidence(string Name)
{
    public CheckStatus Status { get; set; } = CheckStatus.NotRun;
    public string? Message { get; set; }
    public string? ToolVersion { get; set; }
    public int? ExitCode { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public List<string> Commands { get; set; } = [];
    public string Output { get; set; } = "";
}

public sealed record FileChange(string Path, string? OldPath, ChangeKind Kind, int? Added, int? Removed)
{
    public string Diff { get; set; } = "";
    public string? Before { get; set; }
    public string? After { get; set; }
    public List<int> AddedLines { get; set; } = [];
}

public sealed record SourceDocument(string Project, string Path, string Text, string Revision = "Head");
public sealed record MemberEvidence(string Id, string Name, string Kind, string Path, int Line, int EndLine, string Text, string Fingerprint)
{
    public string Change { get; set; } = "Unchanged";
    public bool IsTest { get; set; }
    public string? Before { get; set; }
    public string? BeforePath { get; set; }
    public int? BeforeLine { get; set; }
}
public sealed record TypeEvidence(string Id, string Project, string Name, string FullName, string Kind)
{
    public string? Namespace { get; set; }
    public string Change { get; set; } = "Unchanged";
    public List<string> Files { get; set; } = [];
    public List<MemberEvidence> Members { get; set; } = [];
    public string DeclarationFingerprint { get; set; } = "";
}
public sealed record CodeRelationship(string From, string To, string Kind, string Path, int Line, string Revision);
public sealed record CodeDiagnostic(string Kind, string Category, string Message, bool AffectsCompleteness, string Revision = "Head");
public sealed record CodeEvidence
{
    public List<SourceDocument> Documents { get; set; } = [];
    public string Scope { get; set; } = "C# source declarations in loaded project configurations";
    public bool Complete { get; set; }
    public List<string> Limitations { get; set; } = [];
    public List<CodeDiagnostic> Diagnostics { get; set; } = [];
    public List<TypeEvidence> Types { get; set; } = [];
    public List<CodeRelationship> Relationships { get; set; } = [];
}
public sealed record TestExecution(string Id, string Name, string ClassName, string MethodName, string Assembly, string Outcome,
    string Message, string StackTrace, string Output, double DurationSeconds);
public sealed record FormattingFinding(string Path, int Line, int Column, string Id, string Message, string Severity);
public sealed record CoverageLineEvidence(int Number, long Hits, int BranchesCovered, int BranchesTotal);
public sealed record CoverageFileEvidence(string Path, List<int> ExecutableLines, List<int> CoveredLines)
{
    // Null distinguishes earlier snapshots from a measured file with no executable lines.
    public List<CoverageLineEvidence>? Lines { get; init; }
    public int? BranchesCovered { get; init; }
    public int? BranchesTotal { get; init; }
    public int? MethodsCovered { get; init; }
    public int? MethodsTotal { get; init; }
    public string? Source { get; set; }
}
public sealed record CoverageScopeEvidence(string Name, CoverageEvidence Evidence);
public sealed record CoverageArtifactEvidence(string Name, string? DuplicateOf);
public sealed record CoverageEvidence
{
    public List<CoverageArtifactEvidence> Artifacts { get; set; } = [];
    public List<CoverageScopeEvidence> Scopes { get; set; } = [];
    public bool Available { get; set; }
    public string? Limitation { get; set; }
    public string? Aggregation { get; set; }
    public List<CoverageFileEvidence> Files { get; set; } = [];
    public int? BranchesCovered { get; set; }
    public int? BranchesTotal { get; set; }
    public int? MethodsCovered { get; set; }
    public int? MethodsTotal { get; set; }
}
public sealed record ReviewEvidence
{
    public required ComparisonContext Comparison { get; init; }
    public required ReviewSettings Settings { get; init; }
    public string GitVersion { get; set; } = "";
    public string SourceConsistency { get; set; } = "Collection in progress";
    public bool SourceMatches { get; set; }
    public List<FileChange> Files { get; set; } = [];
    public CheckEvidence Build { get; set; } = new("Build");
    public CheckEvidence Tests { get; set; } = new("Tests and coverage");
    public CheckEvidence Formatting { get; set; } = new("Formatting");
    public List<TestExecution> Executions { get; set; } = [];
    public CoverageEvidence Coverage { get; set; } = new();
    public List<FormattingFinding> FormattingFindings { get; set; } = [];
    public List<string> FormattingConfiguration { get; set; } = [];
    public CodeEvidence Code { get; set; } = new();
}
public sealed record ReviewSnapshot(ReviewRecord Record, ReviewEvidence? Evidence);
public sealed record ProcessRequest(string Executable, IReadOnlyList<string> Arguments, string WorkingDirectory);
public sealed record ProcessResult(int ExitCode, string Output, string Error);
