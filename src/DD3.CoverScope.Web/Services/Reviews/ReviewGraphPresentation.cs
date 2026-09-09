using DD3.CoverScope.Models.Reviews;

namespace DD3.CoverScope.Services.Reviews;

// A graph of saved comparison evidence, independent of the current selection and camera.
public static class ReviewGraphPresentation
{
    public sealed record Edge(string From, string To, string Kind, string Change, List<CodeRelationship> Occurrences);
    public sealed record Node(TypeEvidence Type, int X, int Y, int Connections);
    public sealed record Group(string Id, int X, int Y, int Width, int Height, List<string> Members);
    public sealed record Layout(List<Node> Nodes, List<Edge> Edges, string? HubId, int Width, int Height)
    {
        public List<Group> Groups { get; init; } = [];
    }

    public static Layout Build(CodeEvidence code, string? initialType = null, bool changedOnly = false)
    {
        var types = code.Types.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var changed = code.Types.Where(x => changedOnly ? x.Change is "Added" or "Modified" or "Deleted"
            : x.Change != "Unchanged" || x.Id == initialType).Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        var relationships = code.Relationships.Where(x => types.ContainsKey(x.From) && types.ContainsKey(x.To)
            && x.Revision is "Head" or "Baseline").ToList();
        var included = changedOnly ? changed : relationships.Where(x => changed.Contains(x.From) || changed.Contains(x.To))
            .SelectMany(x => new[] { x.From, x.To }).Concat(changed).ToHashSet(StringComparer.Ordinal);
        if (!changedOnly)
        {
            // Include the complete implementation group of every visible interface or class.
            // These are declaration relationships, not dependency-injection bindings.
            bool expanded;
            do
            {
                expanded = false;
                foreach (var relation in relationships.Where(x => x.Kind == "Implements"))
                {
                    if (!included.Contains(relation.From) && !included.Contains(relation.To)) { continue; }
                    expanded |= included.Add(relation.From);
                    expanded |= included.Add(relation.To);
                }
            } while (expanded);
        }
        var edges = relationships.Where(x => included.Contains(x.From) && included.Contains(x.To))
            .GroupBy(x => (x.From, x.To, x.Kind))
            .Select(group => new Edge(group.Key.From, group.Key.To, group.Key.Kind,
                group.Any(x => x.Revision == "Head") ? group.Any(x => x.Revision == "Baseline") ? "Retained" : "Added" : "Removed",
                group.Distinct().OrderBy(x => x.Revision).ThenBy(x => x.Path, StringComparer.Ordinal).ThenBy(x => x.Line).ToList()))
            .OrderBy(x => x.From, StringComparer.Ordinal).ThenBy(x => x.To, StringComparer.Ordinal).ThenBy(x => x.Kind, StringComparer.Ordinal).ToList();
        var neighbours = included.ToDictionary(x => x, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var edge in edges.Where(x => x.From != x.To))
        { neighbours[edge.From].Add(edge.To); neighbours[edge.To].Add(edge.From); }
        var ordered = included.OrderByDescending(x => neighbours[x].Count).ThenBy(x => types[x].FullName, StringComparer.Ordinal)
            .ThenBy(x => x, StringComparer.Ordinal).ToList();

        // Implementation groups are layout units only. Each type retains its ID and all
        // dependency edges retain their exact endpoints, including concrete dependencies.
        var parents = included.ToDictionary(x => x, x => x, StringComparer.Ordinal);
        string Root(string id)
        {
            while (parents[id] != id) { parents[id] = parents[parents[id]]; id = parents[id]; }
            return id;
        }
        foreach (var edge in edges.Where(x => x.Kind == "Implements"))
        {
            var from = Root(edge.From); var to = Root(edge.To);
            if (from != to) { parents[StringComparer.Ordinal.Compare(from, to) < 0 ? to : from] = StringComparer.Ordinal.Compare(from, to) < 0 ? from : to; }
        }
        var memberships = included.GroupBy(Root).ToDictionary(x => x.Key,
            x => x.OrderBy(id => types[id].FullName, StringComparer.Ordinal).ThenBy(id => id, StringComparer.Ordinal).ToList(), StringComparer.Ordinal);
        var units = memberships.ToDictionary(x => x.Key, x => MakeUnit(x.Key, x.Value, types, neighbours), StringComparer.Ordinal);
        var unitNeighbours = units.Keys.ToDictionary(x => x, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            var from = Root(edge.From); var to = Root(edge.To);
            if (from != to) { unitNeighbours[from].Add(to); unitNeighbours[to].Add(from); }
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var components = new List<(List<Node> Nodes, List<Group> Groups, int Width, int Height)>();
        foreach (var root in ordered.Select(Root).Distinct())
        {
            if (!seen.Add(root)) { continue; }
            var queue = new Queue<string>();
            queue.Enqueue(root);
            var placements = new List<(string Id, int X, int Y)>();
            using var slots = Slots().GetEnumerator();
            while (queue.TryDequeue(out var id))
            {
                slots.MoveNext();
                placements.Add((id, slots.Current.X, slots.Current.Y));
                foreach (var next in unitNeighbours[id].OrderByDescending(x => unitNeighbours[x].Count).ThenBy(x => x, StringComparer.Ordinal))
                { if (seen.Add(next)) { queue.Enqueue(next); } }
            }
            var columns = new Dictionary<int, int>(); var rows = new Dictionary<int, int>();
            var width = 60; var height = 60;
            foreach (var column in placements.GroupBy(x => x.X).OrderBy(x => x.Key))
            { columns[column.Key] = width; width += column.Max(x => units[x.Id].Width) + 80; }
            foreach (var row in placements.GroupBy(x => x.Y).OrderBy(x => x.Key))
            { rows[row.Key] = height; height += row.Max(x => units[x.Id].Height) + 80; }
            var componentNodes = new List<Node>(); var componentGroups = new List<Group>();
            foreach (var placement in placements)
            {
                var unit = units[placement.Id]; var x = columns[placement.X]; var y = rows[placement.Y];
                componentNodes.AddRange(unit.Nodes.Select(n => n with { X = n.X + x, Y = n.Y + y }));
                if (unit.Group is { } group) { componentGroups.Add(group with { X = x, Y = y }); }
            }
            components.Add((componentNodes, componentGroups, width, height));
        }

        var area = components.Sum(c => (double)c.Width * c.Height);
        var shelfWidth = Math.Max(1040, (int)Math.Ceiling(Math.Sqrt(area * 2)));
        var nodes = new List<Node>(); var groups = new List<Group>();
        var left = 0; var top = 0; var rowHeight = 0; var graphWidth = 1040;
        foreach (var component in components)
        {
            if (left > 0 && left + component.Width > shelfWidth) { left = 0; top += rowHeight; rowHeight = 0; }
            nodes.AddRange(component.Nodes.Select(x => x with { X = x.X + left, Y = x.Y + top }));
            groups.AddRange(component.Groups.Select(x => x with { X = x.X + left, Y = x.Y + top }));
            left += component.Width; rowHeight = Math.Max(rowHeight, component.Height); graphWidth = Math.Max(graphWidth, left);
        }
        return new(nodes, edges, ordered.FirstOrDefault(), graphWidth, Math.Max(420, top + rowHeight)) { Groups = groups };
    }

    private sealed record Unit(List<Node> Nodes, Group? Group, int Width, int Height);
    private static Unit MakeUnit(string id, List<string> members, Dictionary<string, TypeEvidence> types, Dictionary<string, HashSet<string>> neighbours)
    {
        if (members.Count == 1) { return new([new(types[id], 20, 20, neighbours[id].Count)], null, 280, 108); }
        var interfaces = members.Where(x => types[x].Kind == "Interface").ToList();
        var implementations = members.Where(x => types[x].Kind != "Interface").ToList();
        var nodes = new List<Node>();
        for (var i = 0; i < interfaces.Count; i++) { var type = interfaces[i]; nodes.Add(new(types[type], 20, 44 + i * 104, neighbours[type].Count)); }
        for (var i = 0; i < implementations.Count; i++) { var type = implementations[i]; nodes.Add(new(types[type], 360, 44 + i * 104, neighbours[type].Count)); }
        var height = 28 + Math.Max(interfaces.Count, implementations.Count) * 104;
        return new(nodes, new(id, 0, 0, 620, height, members), 620, height);
    }

    private static IEnumerable<(int X, int Y)> Slots()
    {
        var x = 0; var y = 0;
        yield return (x, y);
        for (var length = 1; ; length++)
        {
            var direction = length % 2 == 1 ? 1 : -1;
            for (var step = 0; step < length; step++) { x += direction; yield return (x, y); }
            for (var step = 0; step < length; step++) { y += direction; yield return (x, y); }
        }
    }
}
