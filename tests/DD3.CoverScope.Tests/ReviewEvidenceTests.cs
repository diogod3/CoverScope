using System.Text.Json;
using DD3.CoverScope.Analysis;
using DD3.CoverScope.Brokers;
using DD3.CoverScope.Models.Reviews;
using DD3.CoverScope.Services.Reviews;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace DD3.CoverScope.Tests;

public sealed class ReviewEvidenceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "coverscope-review-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void GitNumstatPreservesRenameAndNonTextRecords()
    {
        var changes = GitComparisonService.ParseNumstat("3\t2\t\0old name.cs\0new name.cs\0-\t-\timage.png\0");
        Assert.Equal(3, changes["new name.cs"].Added);
        Assert.Equal(2, changes["new name.cs"].Removed);
        Assert.Null(changes["image.png"].Added);
        Assert.Null(changes["image.png"].Removed);
    }

    [Fact]
    public void GitHunksMapOnlyHeadSideAddedLines()
    {
        var lines = GitComparisonService.AddedLines("--- a/a.cs\n+++ b/a.cs\n@@ -4,3 +4,4 @@\n context\n-removed\n+added\n+second\n context\n@@ -20 +21 @@\n-old\n+new\n");
        Assert.Equal(new[] { 5, 6, 21 }, lines);
    }

    [Fact]
    public void DiffRowsMapCoverageToReviewedLinesAndLeaveRemovedLinesUnmapped()
    {
        var rows = ReviewDiffPresentation.Parse("diff --git a/a.cs b/a.cs\n--- a/a.cs\n+++ b/a.cs\n@@ -4,3 +4,4 @@\n context\n-removed\n+added\n+second\n context\n@@ -20 +21 @@\n-old\n+new\n\\ No newline at end of file\n");
        Assert.Equal(new[] { 4, 0, 5, 6, 7, 0, 0, 21 }, rows.Select(x => x.Number));
        Assert.Equal(new int?[] { 4, 5, null, null, 6, null, 20, null }, rows.Select(x => x.BeforeNumber));
        Assert.Equal(new[] { 5, 6, 21 }, rows.Where(x => x.Change == "added").Select(x => x.Number));
        Assert.All(rows.Where(x => x.Change == "removed"), x => Assert.Equal(0, x.Number));
        Assert.Equal("⋯", rows[5].Text);
        Assert.Equal("new", rows[^1].Text);
    }

    [Fact]
    public void DiffRowsHandleNewAndDeletedFilesWithoutMistakingCodeForHeaders()
    {
        var added = ReviewDiffPresentation.Parse("--- /dev/null\n+++ b/a.cs\n@@ -0,0 +1,2 @@\n+++counter;\n+last\n");
        Assert.Equal(new[] { 1, 2 }, added.Select(x => x.Number));
        Assert.Equal("++counter;", added[0].Text);
        Assert.All(added, x => Assert.Null(x.BeforeNumber));
        var removed = ReviewDiffPresentation.Parse("--- a/a.cs\n+++ /dev/null\n@@ -1,2 +0,0 @@\n---counter;\n-last\n");
        Assert.Equal(new int?[] { 1, 2 }, removed.Select(x => x.BeforeNumber));
        Assert.All(removed, x => Assert.Equal(0, x.Number));
        Assert.Empty(ReviewDiffPresentation.Parse("diff --git a/a.cs b/a.cs\nsimilarity index 100%\nrename from a.cs\nrename to b.cs\n"));
    }

    [Fact]
    public void MemberFingerprintsIgnoreCommentsButPreserveLiteralsAndDirectives()
    {
        static string Fingerprint(string code) => SourceIndexWorker.Fingerprint(CSharpSyntaxTree.ParseText(code).GetRoot().DescendantTokens());
        Assert.Equal(Fingerprint("class C { int M() => 1; }"), Fingerprint("class C\n{ // comment\n int M() => 1; }"));
        Assert.NotEqual(Fingerprint("class C { int M() => 1; }"), Fingerprint("class C { int M() => 2; }"));
        Assert.NotEqual(Fingerprint("class C { int M() => 1; }"), Fingerprint("#nullable enable\nclass C { int M() => 1; }"));
    }

    [Fact]
    public void MemberComparisonCollapsesPartialTypeAndPreservesDeletedMember()
    {
        var old = new CodeEvidence { Complete = true, Types = [Type(Member("same", "original"), Member("deleted", "old"))] };
        var current = new CodeEvidence { Complete = true, Types = [Type(Member("same", "changed"), Member("added", "new"))] };
        var result = CodeComparisonService.Compare(old, current);
        var type = Assert.Single(result.Types);
        Assert.Equal(2, type.Files.Count);
        Assert.Equal("Modified", type.Change);
        Assert.Equal("Modified", type.Members.Single(x => x.Id == "same").Change);
        Assert.Equal("Deleted", type.Members.Single(x => x.Id == "deleted").Change);
        Assert.Equal("Added", type.Members.Single(x => x.Id == "added").Change);
    }

    [Fact]
    public void TestParserPreservesCasesAndExceptionalOutcomes()
    {
        var xml = """
        <TestRun><TestDefinitions><UnitTest id="theory"><TestMethod className="Tests.WorkerTests" name="Theory" codeBase="Tests.dll" /></UnitTest></TestDefinitions>
        <Results><UnitTestResult testId="theory" executionId="one" testName="Theory(1)" outcome="Passed" />
        <UnitTestResult testId="theory" executionId="two" testName="Theory(2)" outcome="Inconclusive" />
        <UnitTestResult testId="theory" executionId="three" testName="Theory(3)" outcome="Aborted"><Output><ErrorInfo><Message>aborted</Message></ErrorInfo></Output></UnitTestResult></Results></TestRun>
        """;
        var results = new ReportReader().ReadTests(xml, "project");
        Assert.Equal(3, results.Count);
        Assert.Equal("Inconclusive", results[1].Outcome);
        Assert.Equal("Aborted", results[2].Outcome);
        Assert.All(results, x => Assert.Equal("Theory", x.MethodName));
    }

    [Fact]
    public void CompatibleCoverageMergesBranchIdentitiesRatherThanCoveredCountMaxima()
    {
        static string Report(int first, int second) => $$"""
        {"module.dll":{"Worker.cs":{"Worker":{"M()":{"Lines":{"10":1},"Branches":[
        {"Line":10,"Offset":1,"EndOffset":2,"Path":0,"Ordinal":0,"Hits":{{first}}},
        {"Line":10,"Offset":1,"EndOffset":3,"Path":1,"Ordinal":1,"Hits":{{second}}}
        ]
        }
        }
        }
        }
        }
        """;
        var result = new ReportReader().ReadCoverageArtifacts([("first", Report(1, 0)), ("second", Report(0, 1))], directory);
        Assert.True(result.Available);
        Assert.Equal(2, result.Scopes.Count);
        Assert.Equal(2, result.BranchesTotal);
        Assert.Equal(2, result.BranchesCovered);
        Assert.Single(Assert.Single(result.Files).CoveredLines);
    }

    [Fact]
    public void CoverageAttachmentCopiesAreCountedOnceAndKeepTheirProvenance()
    {
        const string json = """{"module.dll":{"Worker.cs":{"Worker":{"M()":{"Lines":{"10":1},"Branches":[]}}}}}""";
        var coverage = new ReportReader().ReadCoverageArtifacts([("collector/coverage.json", json), ("trx/In/coverage.json", json)], directory);

        Assert.True(coverage.Available);
        Assert.Single(coverage.Files);
        Assert.Equal(1, coverage.MethodsTotal);
        Assert.Equal(2, coverage.Artifacts.Count);
        Assert.Null(coverage.Artifacts[0].DuplicateOf);
        Assert.Equal("collector/coverage.json", coverage.Artifacts[1].DuplicateOf);
        Assert.Empty(coverage.Scopes);
    }

    [Fact]
    public void DifferentCoverageReportsWithEqualTotalsRemainSeparateWhenInstrumentationDiffers()
    {
        const string first = """{"module.dll":{"Worker.cs":{"Worker":{"M()":{"Lines":{"10":1},"Branches":[]}}}}}""";
        var second = first.Replace("\"10\":1", "\"11\":1", StringComparison.Ordinal);
        var coverage = new ReportReader().ReadCoverageArtifacts([("first", first), ("second", second)], directory);

        Assert.False(coverage.Available);
        Assert.Equal(2, coverage.Scopes.Count);
        Assert.All(coverage.Artifacts, x => Assert.Null(x.DuplicateOf));
        Assert.All(coverage.Scopes, x => Assert.True(x.Evidence.Available));
        Assert.Contains("overlapping", coverage.Limitation ?? "");
    }

    [Theory]
    [InlineData("/packages/microsoft.net.test.sdk/17.11.1/build/netcoreapp3.1/Microsoft.NET.Test.Sdk.Program.cs", "// <auto-generated> SDK entry point", true)]
    [InlineData("C:\\packages\\Microsoft.NET.Test.Sdk\\17.11.1\\build\\netcoreapp3.1\\Microsoft.NET.Test.Sdk.Program.cs", "\uFEFF// <auto-generated> SDK entry point", true)]
    [InlineData("/shared/Microsoft.NET.Test.Sdk.Program.cs", "// <auto-generated> unrelated source", false)]
    [InlineData("/packages/microsoft.net.test.sdk/17.11.1/build/netcoreapp3.1/Microsoft.NET.Test.Sdk.Program.cs", "class Authored { }", false)]
    public void GeneratedTestEntryPointExclusionRequiresPackageIdentityAndGeneratedHeader(string path, string source, bool excluded)
    {
        Assert.Equal(excluded, SourceIndexWorker.IsGeneratedTestEntryPoint(path, source));
    }

    [Fact]
    public void MarkdownExposesPartialEvidenceWithoutEmbeddingTheFullSnapshot()
    {
        var record = Record() with { Status = ReviewStatus.Finished };
        var evidence = Evidence(record);
        evidence.Code.Types.Add(Type(Member("member", "internal-fingerprint")));
        evidence.Code.Limitations.Add("CS0246: App could not be resolved");
        evidence.Coverage.Limitation = "Distinct reports overlap";
        evidence.Executions.Add(new("one", "Example", "Tests", "Example", "Tests.dll", "Failed", "cleanup denied", "fixture stack", "", 1));
        var snapshot = new ReviewSnapshot(record, evidence);
        var exports = new ReviewExportService();

        var markdown = exports.Markdown(snapshot);

        Assert.Contains("Finished means collection ended", markdown);
        Assert.Contains("partial or unavailable", markdown);
        Assert.Contains("counts are unavailable", markdown);
        Assert.Contains("CS0246", markdown);
        Assert.Contains("Coverage: unavailable", markdown);
        Assert.Contains("Distinct reports overlap", markdown);
        Assert.Contains("1 recorded executions — 1 Failed", markdown);
        Assert.Contains("cleanup denied", markdown);
        Assert.Contains("fixture stack", markdown);
        Assert.DoesNotContain("internal-fingerprint", markdown);
        Assert.Contains("internal-fingerprint", exports.Json(snapshot));
        Assert.DoesNotContain("```json", markdown);
    }

    [Fact]
    public void MarkdownReportsAvailableCoverageAndCompleteDeclarationCounts()
    {
        var record = Record() with { Status = ReviewStatus.Finished };
        var evidence = Evidence(record);
        evidence.Code.Complete = true;
        evidence.Code.Types.Add(Type(Member("test", "fingerprint") with { IsTest = true, Change = "Added" }) with { Change = "Added" });
        evidence.Coverage = new() { Available = true, Files = [new("Worker.cs", [10, 11], [10])], BranchesCovered = 0, BranchesTotal = 0, MethodsCovered = 1, MethodsTotal = 1 };

        var markdown = new ReviewExportService().Markdown(new(record, evidence));

        Assert.Contains("1 changed types; 1 changed members; 1 changed xUnit definitions", markdown);
        Assert.Contains("lines 1/2 (50.0%)", markdown);
        Assert.Contains("branches not applicable (0 measured)", markdown);
        Assert.Contains("methods 1/1 (100.0%)", markdown);
    }

    [Fact]
    public void FormatterReportsAreReadWithoutApplyingChanges()
    {
        var json = """[{"FilePath":"Worker.cs","FileChanges":[{"LineNumber":12,"CharNumber":5,"DiagnosticId":"IDE0011","FormatDescription":"Add braces"}]}]""";
        var finding = Assert.Single(new ReportReader().ReadFormatting(json, directory));
        Assert.Equal("IDE0011", finding.Id);
        Assert.Equal(12, finding.Line);
        Assert.Equal("Worker.cs", finding.Path);
    }

    [Fact]
    public async Task CancelledRunHasNoEvidenceOnDiskOrInEitherExport()
    {
        var store = new ReviewStore(new FileSystemBroker(), directory);
        var record = Record();
        await store.SaveRecordAsync(record);
        var evidence = Evidence(record);
        await store.SaveEvidenceAsync(record, evidence, CancellationToken.None);
        var artifact = Path.Combine(store.AnalysisDirectory(record), "completed-test-output.txt");
        await File.WriteAllTextAsync(artifact, "discard-me");
        store.DiscardAnalysis(record);
        record = record with { Status = ReviewStatus.Cancelled, EndedAt = DateTimeOffset.UtcNow };
        await store.SaveRecordAsync(record);
        var snapshot = await store.ReadAsync(record);
        Assert.False(Directory.Exists(store.AnalysisDirectory(record)));
        Assert.Null(snapshot.Evidence);
        // The exporter also protects against a stale caller passing evidence with a cancelled record.
        var stale = new ReviewSnapshot(record, evidence);
        var exports = new ReviewExportService();
        using var json = JsonDocument.Parse(exports.Json(stale));
        Assert.Equal("Cancelled", json.RootElement.GetProperty("status").GetString());
        Assert.False(json.RootElement.TryGetProperty("evidence", out _));
        Assert.DoesNotContain("discard-me", exports.Markdown(stale));
        Assert.DoesNotContain("executions", exports.Markdown(stale));
    }

    [Fact]
    public async Task NewFailureCannotChangeAnEarlierRunSnapshot()
    {
        var store = new ReviewStore(new FileSystemBroker(), directory);
        var first = Record() with { Status = ReviewStatus.Finished };
        var second = Record() with { Status = ReviewStatus.Failed };
        await store.SaveRecordAsync(first);
        await store.SaveEvidenceAsync(first, Evidence(first), CancellationToken.None);
        await store.SaveRecordAsync(second);
        Assert.NotNull((await store.ReadAsync(first)).Evidence);
        Assert.Null((await store.ReadAsync(second)).Evidence);
        Assert.Equal(ReviewStatus.Finished, (await store.ReadAsync(first)).Record.Status);
    }

    [Fact]
    public void SameRepositoryCannotAcquireOverlappingLeases()
    {
        var store = new ReviewStore(new FileSystemBroker(), directory);
        using var lease = store.Acquire(directory);
        Assert.Throws<InvalidOperationException>(() => store.Acquire(directory));
    }

    private ReviewRecord Record() => new(Guid.NewGuid().ToString("N"), directory, Path.Combine(directory, "App.slnx"), "refs/heads/main", DateTimeOffset.UtcNow)
    { Commit = "head", Baseline = "base", TargetCommit = "target" };
    private static ReviewEvidence Evidence(ReviewRecord record) => new()
    {
        Comparison = new(record.Repository, record.TargetPath, "feature", "head", record.TargetRef, "target", "base"),
        Settings = new(), Tests = new("Tests") { Output = "discard-me" }
    };
    private static MemberEvidence Member(string id, string fingerprint) => new(id, id, "Method", "Worker.cs", 1, 2, fingerprint, fingerprint);
    private static TypeEvidence Type(params MemberEvidence[] members) => new("project|Worker", "project", "Worker", "Worker", "Class")
    { Members = members.ToList(), Files = ["Worker.cs", "Worker.Validations.cs"] };
    public void Dispose() { if (Directory.Exists(directory)) { Directory.Delete(directory, true); } }
}
