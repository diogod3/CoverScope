using DD3.CoverScope.Models.Reviews;
using DD3.CoverScope.Services.Reviews;
using Xunit;

namespace DD3.CoverScope.Tests;

public sealed class ReviewWorkspaceTests
{
    [Fact]
    public void PartialNavigationIncludesDeletedMembersAndPrefersChangedFiles()
    {
        var type = Type("Worker") with
        {
            Files = ["A.cs", "B.cs", "A.cs"],
            Members = [Member("Removed", "Deleted.cs") with { Change = "Deleted" }]
        };
        var evidence = Evidence(type);
        evidence.Files.Add(new("B.cs", null, ChangeKind.Modified, 1, 1));
        var navigation = new ReviewWorkspaceNavigation();

        navigation.Open(type, evidence);

        Assert.Equal(new[] { "A.cs", "B.cs", "Deleted.cs" }, ReviewWorkspaceNavigation.Files(type));
        Assert.Equal("B.cs", navigation.Current!.File);
        var visit = navigation.Current;
        navigation.Open(type, evidence, "Removed");
        Assert.Same(visit, navigation.Current);
        Assert.Equal("Deleted.cs", navigation.Current!.File);
        Assert.Equal("Removed", navigation.Current.MemberId);
        Assert.False(navigation.CanGoBack);
    }

    [Fact]
    public void HistoryRestoresEachVisitAndDropsForwardRouteAfterNewNavigation()
    {
        var first = Type("First");
        var second = Type("Second");
        var evidence = Evidence(first, second);
        var navigation = new ReviewWorkspaceNavigation();
        navigation.Back();
        navigation.Forward();
        Assert.Null(navigation.Current);
        navigation.Open(first, evidence);
        var original = navigation.Current!;
        original.File = "Partial.cs";
        original.MemberId = "Method";
        navigation.Open(second, evidence);
        var secondVisit = navigation.Current;
        navigation.Back();
        Assert.Same(original, navigation.Current);
        Assert.Equal("Partial.cs", navigation.Current!.File);
        Assert.Equal("Method", navigation.Current.MemberId);
        navigation.Forward();
        Assert.Same(secondVisit, navigation.Current);
        navigation.Open(first, evidence);
        Assert.NotEqual(original.Key, navigation.Current!.Key);
        navigation.Back();
        navigation.Open(Type("Third"), evidence);
        Assert.False(navigation.CanGoForward);
        navigation.Back();
        Assert.Same(secondVisit, navigation.Current);
    }

    [Fact]
    public void RelatedDefinitionsUseStaticEdgesAndKeepParameterizedExecutionsTogether()
    {
        var worker = Type("Worker");
        var tests = Type("WorkerTests") with { Members = [Member("Theory", "Tests.cs") with { IsTest = true }] };
        var unrelated = Type("OtherTests") with { Members = [Member("Other", "Other.cs") with { IsTest = true }] };
        var evidence = Evidence(worker, tests, unrelated);
        evidence.Code.Relationships.Add(new(tests.Id, worker.Id, "calls", "Tests.cs", 1, "Head"));
        evidence.Executions = [Execution("one", tests.FullName), Execution("two", tests.FullName + ", TestsAssembly")];

        var definition = Assert.Single(ReviewTestNavigation.Related(evidence, worker));

        Assert.Equal(tests.Id, definition.Type.Id);
        Assert.Equal(2, ReviewTestNavigation.Executions(evidence, definition).Count);
        Assert.Single(ReviewTestNavigation.Related(evidence, tests));
    }

    [Fact]
    public void ExecutionLinksDoNotGuessForDeletedOrAmbiguousDefinitions()
    {
        var test = Member("Theory", "Tests.cs") with { IsTest = true };
        var type = Type("Tests") with { Members = [test] };
        var evidence = Evidence(type);
        evidence.Executions = [Execution("one", type.FullName)];
        var definition = new ReviewTestNavigation.Definition(type, test);
        Assert.Single(ReviewTestNavigation.Executions(evidence, definition));

        test.Change = "Deleted";
        Assert.Empty(ReviewTestNavigation.Executions(evidence, definition));
        test.Change = "Unchanged";
        type.Members.Add(test with { Id = "overload" });
        Assert.Empty(ReviewTestNavigation.Executions(evidence, definition));
        type.Members.RemoveAt(1);
        evidence.Code.Types.Add(type with { Id = "other-project|Tests", Project = "Other" });
        Assert.Empty(ReviewTestNavigation.Executions(evidence, definition));
    }

    private static ReviewEvidence Evidence(params TypeEvidence[] types) => new()
    {
        Comparison = new("repo", "App.sln", "feature", "head", "main", "target", "base"),
        Settings = new(), Code = new() { Types = types.ToList() }
    };
    private static TypeEvidence Type(string name) => new(name, "App", name, "App." + name, "Class") { Files = [name + ".cs"] };
    private static MemberEvidence Member(string name, string path) => new(name, name, "Method", path, 10, 20, "", "");
    private static TestExecution Execution(string id, string name) => new(id, "Theory(" + id + ")", name, "Theory", "Tests.dll", "Passed", "", "", "", 0);
}
