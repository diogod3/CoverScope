namespace DD3.CoverScope.Brokers.FileSystems;

public partial class FileSystemBroker
{
    public bool FileExists(string path) => File.Exists(path);

    public IReadOnlyList<string> EnumerateFiles(string directoryPath) =>
        Directory.GetFiles(directoryPath);
}
