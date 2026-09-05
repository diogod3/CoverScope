using DD3.CoverScope;

namespace DD3.CoverScope.Tests;

public partial class CoverScopeCommandLineTests
{
    // Parsing must not require this directory or a target file to exist.
    private readonly string directory =
        Path.Combine(Path.GetTempPath(), $"coverscope-cli-{Guid.NewGuid():N}");

    private CoverScopeCommandLineResult Parse(params string[] arguments) =>
        CoverScopeCommandLine.Parse(arguments, directory);
}
