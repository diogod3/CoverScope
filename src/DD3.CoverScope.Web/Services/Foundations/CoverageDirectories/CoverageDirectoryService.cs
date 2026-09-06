using DD3.CoverScope.Models;
using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Brokers.FileSystems;
using DD3.CoverScope.Models.CoverageTargets;
using DD3.CoverScope.Models.CoverageTargets.Exceptions;
using DD3.CoverScope.Services.Foundations.CoverageTargets;

namespace DD3.CoverScope.Services.Foundations.CoverageDirectories;

public partial class CoverageDirectoryService : ICoverageDirectoryService
{
    public SolutionBrowserSnapshot Open(string? currentSelection = null) => Trace(() => TryCatch(() => ValueTask.FromResult(OpenCore(currentSelection)))).GetAwaiter().GetResult();

    public SolutionBrowserSnapshot Browse(string directoryPath) => Trace(() => TryCatch(() => ValueTask.FromResult(BrowseCore(directoryPath)))).GetAwaiter().GetResult();

    public IReadOnlyList<SolutionBrowserLocation> GetLocations() => Trace(() => TryCatch(() => ValueTask.FromResult(GetLocationsCore()))).GetAwaiter().GetResult();

    private readonly IDiagnosticsBroker diagnosticsBroker;
    private readonly string initialDirectory;
    private readonly IFileSystemBroker fileSystemBroker;

    public CoverageDirectoryService(
        string initialDirectory,
        IFileSystemBroker fileSystemBroker,
        IDiagnosticsBroker diagnosticsBroker)
    {
        this.diagnosticsBroker = diagnosticsBroker;
        this.initialDirectory = Path.GetFullPath(initialDirectory);
        this.fileSystemBroker = fileSystemBroker;
    }

    private SolutionBrowserSnapshot OpenCore(string? currentSelection = null) =>
        BrowseCore(ResolveInitialDirectory(currentSelection));

    private SolutionBrowserSnapshot BrowseCore(string directoryPath)
    {
        try
        {
            ValidateDirectoryPath(directoryPath);
            var fullPath = Path.GetFullPath(directoryPath, initialDirectory);
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

    private IReadOnlyList<SolutionBrowserLocation> GetLocationsCore()
    {
        var locations = new List<SolutionBrowserLocation>();
        AddLocation(locations, "Home", fileSystemBroker.GetUserDirectory());
        AddLocation(locations, "Working directory", initialDirectory);

        try
        {
            foreach (var drive in fileSystemBroker.GetReadyDrives())
                AddLocation(locations, drive, drive);
        }
        catch (UnauthorizedAccessException)
        {
            // Home and the current directory remain available if volumes cannot be listed.
        }

        return locations;
    }



    internal static bool IsSupportedFile(string path) =>
        CoverageTargetFileExtensions.TryGetTargetType(path, out _);

    private string ResolveInitialDirectory(string? currentSelection)
    {
        if (!string.IsNullOrWhiteSpace(currentSelection))
        {
            try
            {
                var fullPath = Path.GetFullPath(currentSelection, initialDirectory);
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
        var home = fileSystemBroker.GetUserDirectory();
        return fileSystemBroker.DirectoryExists(home) ? home : initialDirectory;
    }

    private void AddLocation(List<SolutionBrowserLocation> locations, string label, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !fileSystemBroker.DirectoryExists(path)) return;
        var fullPath = Path.GetFullPath(path);
        if (locations.Any(location => string.Equals(location.Path, fullPath, StringComparison.OrdinalIgnoreCase))) return;
        locations.Add(new(label, fullPath));
    }
}
