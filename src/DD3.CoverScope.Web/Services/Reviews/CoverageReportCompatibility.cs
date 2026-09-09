using System.Text.Json;

namespace DD3.CoverScope.Services.Reviews;

// Checks reported instrumentation identities, not hit counts or aggregate totals.
// Coverlet JSON has no binary checksum: this is structural compatibility within a run.
internal sealed class CoverageReportCompatibility
{
    private readonly Dictionary<string, HashSet<string>> modules = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> sourceModules = new(StringComparer.Ordinal);

    public string? Include(string json, string root)
    {
        using var document = JsonDocument.Parse(json);
        string? conflict = null;
        foreach (var module in document.RootElement.EnumerateObject())
        {
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in module.Value.EnumerateObject())
            {
                var path = ReportReader.NormalizePath(source.Name, root);
                identities.Add(Key("Source", path));
                if (!sourceModules.TryGetValue(path, out var owners)) { sourceModules[path] = owners = new(StringComparer.Ordinal); }
                owners.Add(module.Name);
                if (owners.Count > 1) { conflict ??= "Source is reported under different modules: " + path; }
                foreach (var type in source.Value.EnumerateObject())
                {
                    identities.Add(Key("Type", path, type.Name));
                    foreach (var method in type.Value.EnumerateObject())
                    {
                        identities.Add(Key("Method", path, type.Name, method.Name));
                        if (!method.Value.TryGetProperty("Lines", out var lines) || !method.Value.TryGetProperty("Branches", out var branches))
                        { conflict ??= "Incomplete instrumentation metadata in module: " + module.Name; continue; }
                        foreach (var line in lines.EnumerateObject())
                        { identities.Add(Key("Line", path, type.Name, method.Name, line.Name)); }
                        foreach (var branch in branches.EnumerateArray())
                        {
                            identities.Add(Key("Branch", path, type.Name, method.Name,
                                branch.GetProperty("Line").GetInt32(), branch.GetProperty("Offset").GetInt32(),
                                branch.GetProperty("EndOffset").GetInt32(), branch.GetProperty("Path").GetInt32(), branch.GetProperty("Ordinal").GetInt32()));
                        }
                    }
                }
            }
            if (modules.TryGetValue(module.Name, out var previous) && !previous.SetEquals(identities))
            { conflict ??= "Instrumentation identities differ for module: " + module.Name; }
            else { modules[module.Name] = identities; }
        }
        return conflict;
    }

    private static string Key(params object[] parts) => JsonSerializer.Serialize(parts);
}
