using System.Text;

namespace DD3.CoverScope.Brokers.FileSystems;

public partial class FileSystemBroker
{
    public virtual bool FileExists(string path) => File.Exists(path);
    public virtual IReadOnlyList<string> EnumerateFiles(string directoryPath) => Directory.GetFiles(directoryPath);
    public virtual IReadOnlyList<string> EnumerateFiles(string directoryPath, string pattern, SearchOption option) =>
        Directory.GetFiles(directoryPath, pattern, option);
    public virtual Stream OpenRead(string path) => File.OpenRead(path);
    public virtual string ReadAllText(string path) => File.ReadAllText(path);
    public virtual IReadOnlyList<string> ReadAllLines(string path) => File.ReadAllLines(path);
    public virtual DateTimeOffset GetLastWriteTime(string path) => new(File.GetLastWriteTimeUtc(path));
    public virtual async ValueTask<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) =>
        await File.ReadAllTextAsync(path, cancellationToken);
    public virtual void WriteAllText(string path, string text) => File.WriteAllText(path, text);
    public virtual void CreateNewFile(string path)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
    }
    public virtual void DeleteFile(string path) => File.Delete(path);

    public virtual async ValueTask WriteAllTextAsync(string path, string text, CancellationToken cancellationToken = default)
    {
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes(text));
        await WriteStreamAsync(path, input, cancellationToken);
    }

    // Atomic replacement on the same volume preserves the previous file if writing fails.
    public virtual async ValueTask WriteStreamAsync(string path, Stream input, CancellationToken cancellationToken = default)
    {
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await input.CopyToAsync(output, cancellationToken);
                await output.FlushAsync(cancellationToken);
                output.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            // Cleanup must not mask the original failure.
            try { File.Delete(temporaryPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
