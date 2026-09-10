using DD3.CoverScope.Models.Reviews;
using DD3.CoverScope.Services.Reviews;
using Xunit;

namespace DD3.CoverScope.Tests;

public sealed class ReviewGraphTests
{
    [Fact]
    public void HidingTestsRemovesTheirEdgesWithoutRemovingHelpersOrReintroducingTestsThroughGroups()
    {
        var service = Type("service", "App.Service");
        var contract = Type("contract", "App.IService", "Interface");
        var tests = Type("tests", "App.ServiceTests");
        tests.Members.Add(new("test", "Works", "Method", "Tests.cs", 1, 2, "", "") { IsTest = true });
        var helper = Type("helper", "App.TestHelper");
        var code = new CodeEvidence
        {
            Types = [service, contract, tests, helper],
            Relationships = [new("service", "contract", "Implements", "Service.cs", 1, "Head"),
                new("tests", "contract", "Implements", "Tests.cs", 1, "Baseline"),
                new("tests", "service", "Calls", "Tests.cs", 2, "Head")]
        };
        Assert.Equal(4, ReviewGraphPresentation.Build(code).Nodes.Count);

        foreach (var changedOnly in new[] { false, true })
        {
            var graph = ReviewGraphPresentation.Build(code, tests.Id, changedOnly, showTests: false);
            Assert.Equal(new[] { "contract", "helper", "service" }, graph.Nodes.Select(x => x.Type.Id).Order());
            Assert.DoesNotContain(graph.Edges, x => x.From == tests.Id || x.To == tests.Id);
            Assert.DoesNotContain(graph.Groups, x => x.Members.Contains(tests.Id));
            Assert.All(graph.Edges, x =>
            {
                Assert.Contains(graph.Nodes, n => n.Type.Id == x.From);
                Assert.Contains(graph.Nodes, n => n.Type.Id == x.To);
            });
        }
        Assert.Equal(4, code.Types.Count);
        Assert.Equal(3, code.Relationships.Count);
    }

    [Fact]
    public void SearchMatchesNamesAndNamespacesOnlyWithinTheVisibleGraph()
    {
        var service = Type("service", "App.Service");
        var contract = Type("contract", "App.IService", "Interface");
        var unchanged = Type("unchanged", "App.ServiceFactory");
        unchanged.Change = "Unchanged";
        var tests = Type("tests", "App.ServiceTests");
        tests.Members.Add(new("test", "Works", "Method", "Tests.cs", 1, 2, "", "") { IsTest = true });
        var graph = ReviewGraphPresentation.Build(new() { Types = [tests, service, unchanged, contract] }, changedOnly: true, showTests: false);
        var originalNodes = graph.Nodes.ToArray();

        Assert.Equal(new[] { "contract", "service" }, ReviewGraphPresentation.Search(graph, "  SERVICE ").Select(x => x.Type.Id));
        Assert.Equal(2, ReviewGraphPresentation.Search(graph, "app.").Count);
        Assert.Single(ReviewGraphPresentation.Search(graph, "App.Service"));
        Assert.Empty(ReviewGraphPresentation.Search(graph, "Tests"));
        Assert.Empty(ReviewGraphPresentation.Search(graph, "Factory"));
        Assert.Empty(ReviewGraphPresentation.Search(graph, " "));
        Assert.Equal(originalNodes, graph.Nodes);
    }

    private static TypeEvidence Type(string id, string name, string kind = "Class") => new(id, "project", name.Split('.').Last(), name, kind) { Change = "Added" };
}
