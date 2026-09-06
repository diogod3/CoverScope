namespace DD3.CoverScope.Services.Views;
public partial class SolutionBrowserViewService
{
    private static void ValidatePath(string path) => ArgumentException.ThrowIfNullOrWhiteSpace(path);
}
