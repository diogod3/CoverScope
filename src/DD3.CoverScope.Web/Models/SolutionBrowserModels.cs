namespace DD3.CoverScope.Models;

public record SolutionBrowserLocation(string Label, string Path);

public record SolutionBrowserEntry(string Name, string Path, bool IsDirectory);

public record SolutionBrowserSnapshot(
    string DirectoryPath,
    string? ParentPath,
    IReadOnlyList<SolutionBrowserEntry> Entries,
    string? ErrorMessage = null);

public record SolutionSelectionResult(bool Success, string? SelectedPath, string Message);
