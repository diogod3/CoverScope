using System.Collections.Concurrent;

namespace DD3.CoverScope.Services.Reviews;

internal sealed class SourceObservation : IDisposable
{
    private readonly FileSystemWatcher? watcher;
    private readonly string root;
    private readonly ConcurrentDictionary<string, byte> touched = new(StringComparer.Ordinal);
    private volatile bool failed;

    public SourceObservation(string root)
    {
        this.root = root;
        try
        {
            watcher = new(root) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size };
            watcher.Changed += Changed;
            watcher.Created += Changed;
            watcher.Deleted += Changed;
            watcher.Renamed += (_, e) => { Remember(e.OldFullPath); Remember(e.FullPath); };
            watcher.Error += (_, _) => failed = true;
            watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException) { failed = true; }
    }
    public bool SourceTouched(IEnumerable<string> paths) => failed || paths.Any(touched.ContainsKey);
    private void Changed(object sender, FileSystemEventArgs e) => Remember(e.FullPath);
    private void Remember(string path) => touched.TryAdd(Path.GetRelativePath(root, path).Replace('\\', '/'), 0);
    public void Dispose() => watcher?.Dispose();
}
