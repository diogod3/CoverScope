namespace DD3.CoverScope.Brokers.FileSystems;

public partial interface IFileSystemBroker
{
    bool DirectoryExists(string path);

    IReadOnlyList<string> EnumerateDirectories(string directoryPath);
}
