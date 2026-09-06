namespace DD3.CoverScope.Brokers.FileSystems;

public partial class FileSystemBroker
{
    public virtual bool DirectoryExists(string path) => Directory.Exists(path);
    public virtual IReadOnlyList<string> EnumerateDirectories(string path) => Directory.GetDirectories(path);
    public virtual void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public virtual string GetUserDirectory() => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public virtual string GetApplicationDataDirectory() => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    public virtual IReadOnlyList<string> GetReadyDrives()
    {
        var paths = new List<string>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try { if (drive.IsReady) paths.Add(drive.RootDirectory.FullName); }
            catch (IOException) { }
        }
        return paths;
    }
}
