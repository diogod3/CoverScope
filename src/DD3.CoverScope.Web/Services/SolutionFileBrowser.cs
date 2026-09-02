namespace DD3.CoverScope.Services;

public sealed record SolutionBrowserLocation(string Label, string Path);

public sealed record SolutionBrowserEntry(string Name, string Path, bool IsDirectory);

public sealed record SolutionBrowserSnapshot(
    string DirectoryPath,
    string? ParentPath,
    IReadOnlyList<SolutionBrowserEntry> Entries,
    string? ErrorMessage = null);

public sealed record SolutionSelectionResult(bool Success, string? SelectedPath, string Message);

public sealed class SolutionFileBrowser
{
    private static readonly string[] AllowedExtensions = [".sln", ".slnx", ".csproj", ".fsproj", ".vbproj"];
    private readonly string initialDirectory;

    public SolutionFileBrowser() : this(Environment.CurrentDirectory) { }

    public SolutionFileBrowser(string initialDirectory) =>
        this.initialDirectory = Path.GetFullPath(initialDirectory);

    public SolutionBrowserSnapshot Open(string? currentSelection = null) =>
        Browse(ResolveInitialDirectory(currentSelection));

    public SolutionBrowserSnapshot Browse(string directoryPath)
    {
        try
        {
            var fullPath = Path.GetFullPath(directoryPath);
            if (!Directory.Exists(fullPath))
                return new(fullPath, null, [], "That folder does not exist or is not accessible.");

            var entries = new List<SolutionBrowserEntry>();
            foreach (var directory in Directory.EnumerateDirectories(fullPath))
            {
                var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(directory));
                entries.Add(new(name.Length == 0 ? directory : name, directory, true));
            }

            foreach (var file in Directory.EnumerateFiles(fullPath).Where(IsSupportedFile))
                entries.Add(new(Path.GetFileName(file), file, false));

            var ordered = entries
                .OrderByDescending(entry => entry.IsDirectory)
                .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return new(fullPath, Directory.GetParent(fullPath)?.FullName, ordered);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new(directoryPath, null, [], $"This folder could not be opened: {ex.Message}");
        }
    }

    public IReadOnlyList<SolutionBrowserLocation> GetLocations()
    {
        var locations = new List<SolutionBrowserLocation>();
        AddLocation(locations, "Home", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        AddLocation(locations, "Working directory", initialDirectory);

        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.IsReady)
                        AddLocation(locations, drive.Name, drive.RootDirectory.FullName);
                }
                catch (IOException)
                {
                    // A removable or network volume can disappear while locations are read.
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Home and the current directory remain available if volumes cannot be listed.
        }

        return locations;
    }

    public SolutionSelectionResult ValidateSelection(string selectedPath)
    {
        try
        {
            var fullPath = Path.GetFullPath(selectedPath);
            if (!File.Exists(fullPath))
                return new(false, null, "The selected solution or project no longer exists.");
            if (!IsSupportedFile(fullPath))
                return new(false, null, "Select a .sln, .slnx, .csproj, .fsproj, or .vbproj file.");
            return new(true, fullPath, "Solution selected.");
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new(false, null, "The selected path is invalid.");
        }
    }

    internal static bool IsSupportedFile(string path) =>
        AllowedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    private string ResolveInitialDirectory(string? currentSelection)
    {
        if (!string.IsNullOrWhiteSpace(currentSelection))
        {
            try
            {
                var fullPath = Path.GetFullPath(currentSelection);
                if (Directory.Exists(fullPath)) return fullPath;
                if (File.Exists(fullPath)) return Path.GetDirectoryName(fullPath) ?? initialDirectory;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                _ = ex;
                // Fall through to a safe starting folder.
            }
        }

        if (Directory.Exists(initialDirectory)) return initialDirectory;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Directory.Exists(home) ? home : Environment.CurrentDirectory;
    }

    private static void AddLocation(List<SolutionBrowserLocation> locations, string label, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
        var fullPath = Path.GetFullPath(path);
        if (locations.Any(location => string.Equals(location.Path, fullPath, StringComparison.OrdinalIgnoreCase))) return;
        locations.Add(new(label, fullPath));
    }
}
