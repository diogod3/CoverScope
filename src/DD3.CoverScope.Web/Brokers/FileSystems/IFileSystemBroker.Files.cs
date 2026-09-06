namespace DD3.CoverScope.Brokers.FileSystems;

public partial interface IFileSystemBroker
{
    bool FileExists(string path);
    IReadOnlyList<string> EnumerateFiles(string directoryPath);
    IReadOnlyList<string> EnumerateFiles(string directoryPath, string pattern, SearchOption option);
    Stream OpenRead(string path);
    string ReadAllText(string path);
    IReadOnlyList<string> ReadAllLines(string path);
    DateTimeOffset GetLastWriteTime(string path);
    ValueTask<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);
    ValueTask WriteAllTextAsync(string path, string text, CancellationToken cancellationToken = default);
    ValueTask WriteStreamAsync(string path, Stream input, CancellationToken cancellationToken = default);
    void WriteAllText(string path, string text);
    void CreateNewFile(string path);
    void DeleteFile(string path);
}
