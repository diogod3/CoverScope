namespace DD3.CoverScope.Brokers.FileSystems;

public partial class FileSystemBroker
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public IReadOnlyList<string> EnumerateDirectories(string directoryPath) =>
        Directory.GetDirectories(directoryPath);
}
