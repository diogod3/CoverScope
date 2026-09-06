namespace DD3.CoverScope.Services.Foundations.CoverageDirectories;
public partial class CoverageDirectoryService
{
    private static void ValidateDirectoryPath(string path) => ArgumentException.ThrowIfNullOrWhiteSpace(path);
}
