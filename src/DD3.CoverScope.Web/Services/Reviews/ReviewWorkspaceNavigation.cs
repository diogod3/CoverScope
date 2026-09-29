using DD3.CoverScope.Models.Reviews;

namespace DD3.CoverScope.Services.Reviews;

// In-memory navigation for one opened review; never part of persisted evidence.
public sealed class ReviewWorkspaceNavigation
{
    public sealed class Visit(TypeEvidence type, string? file, string? memberId)
    {
        public string Key { get; } = Guid.NewGuid().ToString("N");
        public TypeEvidence Type { get; } = type;
        public string? File { get; set; } = file;
        public string? MemberId { get; set; } = memberId;
    }
    private readonly List<Visit> visits = [];
    private int index = -1;
    public Visit? Current => index < 0 ? null : visits[index];
    public bool CanGoBack => index > 0;
    public bool CanGoForward => index + 1 < visits.Count;
    public void Open(TypeEvidence type, ReviewEvidence evidence, string? memberId = null)
    {
        if (Current?.Type.Id == type.Id)
        {
            if (memberId is not null && type.Members.FirstOrDefault(x => x.Id == memberId) is { } member)
            { Current.File = member.Path; Current.MemberId = member.Id; }
            return;
        }
        var selected = type.Members.FirstOrDefault(x => x.Id == memberId);
        var file = selected?.Path ?? Files(type).FirstOrDefault(path => evidence.Files.Any(x => x.Path == path || x.OldPath == path)) ?? Files(type).FirstOrDefault();
        if (CanGoForward) { visits.RemoveRange(index + 1, visits.Count - index - 1); }
        visits.Add(new(type, file, selected?.Id));
        index = visits.Count - 1;
    }
    public void Back() { if (CanGoBack) { index--; } }
    public void Forward() { if (CanGoForward) { index++; } }
    public sealed record FileTab(string Path, string Label);
    public static IReadOnlyList<string> Files(TypeEvidence type) => FileTabs(type).Select(x => x.Path).ToArray();
    public static IReadOnlyList<FileTab> FileTabs(TypeEvidence type)
    {
        var files = type.Files.Concat(type.Members.Select(x => x.Path)).Distinct(StringComparer.Ordinal)
            .Select(path =>
            {
                var name = path.Replace('\\', '/').Split('/')[^1];
                var main = name == type.Name + ".cs";
                var partial = name.StartsWith(type.Name + ".", StringComparison.Ordinal) && name.EndsWith(".cs", StringComparison.Ordinal);
                var label = main ? type.Name : partial ? name[(type.Name.Length + 1)..^3] : name;
                return new { Path = path, Label = label, Order = main ? 0 : partial ? 1 : 2 };
            }).OrderBy(x => x.Order).ThenBy(x => x.Label, StringComparer.Ordinal).ThenBy(x => x.Path, StringComparer.Ordinal).ToArray();
        var duplicateLabels = files.GroupBy(x => x.Label, StringComparer.Ordinal).Where(x => x.Count() > 1).Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
        return files.Select(x => new FileTab(x.Path, duplicateLabels.Contains(x.Label) ? x.Path : x.Label)).ToArray();
    }
}
