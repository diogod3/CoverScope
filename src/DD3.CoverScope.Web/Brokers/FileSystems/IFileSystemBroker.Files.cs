namespace DD3.CoverScope.Brokers.FileSystems;

public partial interface IFileSystemBroker
{
    bool FileExists(string path);

    IReadOnlyList<string> EnumerateFiles(string directoryPath);
}
