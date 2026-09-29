using DD3.CoverScope.Models.Reviews;

namespace DD3.CoverScope.Services.Reviews;

public static class ReviewTestNavigation
{
    public sealed record Definition(TypeEvidence Type, MemberEvidence Member);
    public static List<Definition> Related(ReviewEvidence evidence, TypeEvidence type)
    {
        var ids = evidence.Code.Relationships.Where(x => x.From == type.Id || x.To == type.Id)
            .SelectMany(x => new[] { x.From, x.To }).Append(type.Id).ToHashSet(StringComparer.Ordinal);
        return evidence.Code.Types.Where(x => ids.Contains(x.Id))
            .SelectMany(t => t.Members.Where(m => m.IsTest).Select(m => new Definition(t, m)))
            .OrderBy(x => x.Type.FullName, StringComparer.Ordinal).ThenBy(x => x.Member.Name, StringComparer.Ordinal).ToList();
    }
    public static List<TestExecution> Executions(ReviewEvidence evidence, Definition test)
    {
        // Never guess across identical type names in different projects or overloaded methods.
        if (test.Member.Change == "Deleted" || test.Type.Change == "Deleted"
            || evidence.Code.Types.Count(t => t.FullName == test.Type.FullName) != 1
            || test.Type.Members.Count(m => m.IsTest && m.Name == test.Member.Name && m.Change != "Deleted") != 1) { return []; }
        return evidence.Executions.Where(x => x.ClassName.Split(',')[0].Trim() == test.Type.FullName && x.MethodName == test.Member.Name).ToList();
    }
}
