using DD3.CoverScope.Brokers.FileSystems;
using DD3.CoverScope.Models.CoverageTargets;
using DD3.CoverScope.Models.CoverageTargets.Exceptions;
using DD3.CoverScope.Services.Foundations.CoverageTargets;

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
    private readonly string initialDirectory;
    private readonly IFileSystemBroker fileSystemBroker;
    private readonly ICoverageTargetService targetService;

    public SolutionFileBrowser(
        string initialDirectory,
        IFileSystemBroker fileSystemBroker,
        ICoverageTargetService targetService)
    {
        this.initialDirectory = Path.GetFullPath(initialDirectory);
        this.fileSystemBroker = fileSystemBroker;
        this.targetService = targetService;
    }

    public SolutionBrowserSnapshot Open(string? currentSelection = null) =>
        Browse(ResolveInitialDirectory(currentSelection));

    public SolutionBrowserSnapshot Browse(string directoryPath)
    {
        try
        {
            var fullPath = Path.GetFullPath(directoryPath);
            if (!fileSystemBroker.DirectoryExists(fullPath))
                return new(fullPath, null, [], "That folder does not exist or is not accessible.");

            var entries = new List<SolutionBrowserEntry>();
            foreach (var directory in fileSystemBroker.EnumerateDirectories(fullPath))
            {
                var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(directory));
                entries.Add(new(name.Length == 0 ? directory : name, directory, true));
            }

            foreach (var file in fileSystemBroker.EnumerateFiles(fullPath).Where(IsSupportedFile))
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

    public async ValueTask<SolutionSelectionResult> ValidateSelectionAsync(string selectedPath)
    {
        try
        {
            var target = await targetService.RetrieveCoverageTargetAsync(
                selectedPath, initialDirectory);
            return new(true, target.Path, "Solution selected.");
        }
        catch (CoverageTargetException exception)
        {
            return new(false, null, exception.Message);
        }
    }

    internal static bool IsSupportedFile(string path) =>
        CoverageTargetFileExtensions.TryGetTargetType(path, out _);

    private string ResolveInitialDirectory(string? currentSelection)
    {
        if (!string.IsNullOrWhiteSpace(currentSelection))
        {
            try
            {
                var fullPath = Path.GetFullPath(currentSelection);
                if (fileSystemBroker.DirectoryExists(fullPath)) return fullPath;
                if (fileSystemBroker.FileExists(fullPath)) return Path.GetDirectoryName(fullPath) ?? initialDirectory;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                _ = ex;
                // Fall through to a safe starting folder.
            }
        }

        if (fileSystemBroker.DirectoryExists(initialDirectory)) return initialDirectory;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return fileSystemBroker.DirectoryExists(home) ? home : Environment.CurrentDirectory;
    }

    private void AddLocation(List<SolutionBrowserLocation> locations, string label, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !fileSystemBroker.DirectoryExists(path)) return;
        var fullPath = Path.GetFullPath(path);
        if (locations.Any(location => string.Equals(location.Path, fullPath, StringComparison.OrdinalIgnoreCase))) return;
        locations.Add(new(label, fullPath));
    }
}
