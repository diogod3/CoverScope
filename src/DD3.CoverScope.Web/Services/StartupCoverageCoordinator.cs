namespace DD3.CoverScope.Services;

public sealed class StartupCoverageCoordinator(CoverScopeLaunchContext launchContext)
{
    private int started;

    public bool TryBegin(out string? targetPath)
    {
        targetPath = launchContext.InitialTargetPath;
        return targetPath is not null
            && Interlocked.CompareExchange(ref started, 1, 0) == 0;
    }
}
