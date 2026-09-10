namespace DD3.CoverScope.Brokers;

public interface IFileSystemBroker
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    Task<string> ReadAsync(string path, CancellationToken token = default);
    Task WriteAsync(string path, string text, CancellationToken token = default);
    void Replace(string source, string destination);
    void DeleteFile(string path);
    void DeleteDirectory(string path);
    string[] Files(string path, string pattern, SearchOption option = SearchOption.TopDirectoryOnly);
    string[] Directories(string path);
    IDisposable Lease(string path);
    Task ExtractTarAsync(string source, string directory, CancellationToken token);
}

public sealed class FileSystemBroker : IFileSystemBroker
{
    public bool FileExists(string path) => File.Exists(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public Task<string> ReadAsync(string path, CancellationToken token = default) => File.ReadAllTextAsync(path, token);
    public Task WriteAsync(string path, string text, CancellationToken token = default) => File.WriteAllTextAsync(path, text, token);
    public void Replace(string source, string destination) => File.Move(source, destination, true);
    public void DeleteFile(string path) => File.Delete(path);
    public void DeleteDirectory(string path)
    {
        var directory = new DirectoryInfo(path);
        // Remove links themselves; never traverse into their targets during cleanup.
        if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) { directory.Delete(); return; }
        foreach (var entry in directory.EnumerateFileSystemInfos())
        {
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) { entry.Delete(); }
            else if (entry is DirectoryInfo child) { DeleteDirectory(child.FullName); }
            else { entry.Attributes &= ~FileAttributes.ReadOnly; entry.Delete(); }
        }
        directory.Attributes &= ~FileAttributes.ReadOnly;
        directory.Delete();
    }
    public string[] Files(string path, string pattern, SearchOption option = SearchOption.TopDirectoryOnly) => Directory.GetFiles(path, pattern, option);
    public string[] Directories(string path) => Directory.GetDirectories(path, "*", new EnumerationOptions
    { RecurseSubdirectories = false, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false });
    public Task ExtractTarAsync(string source, string directory, CancellationToken token) => System.Formats.Tar.TarFile.ExtractToDirectoryAsync(source, directory, false, token);
    public IDisposable Lease(string path) => new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
}
