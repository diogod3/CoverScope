using System.Reflection;

namespace DD3.CoverScope;

internal sealed record CoverScopeCommandLineOptions(
    string InvocationDirectory,
    string? TargetPath,
    bool OpenBrowser,
    int? Port,
    bool ShowHelp,
    bool ShowVersion);

internal sealed record CoverScopeCommandLineResult(
    CoverScopeCommandLineOptions? Options,
    string? ErrorMessage)
{
    public bool Success => Options is not null;
}

internal static class CoverScopeCommandLine
{
    public const string HelpText = """
        CoverScope - local .NET code coverage explorer

        Usage:
          coverscope [solution-or-project] [options]

        Arguments:
          solution-or-project  Optional path to a .sln, .slnx, .csproj, .fsproj,
                               or .vbproj file.

        Options:
          --no-browser         Do not open the default browser.
          --port <number>      Listen on a specific port (1-65535).
          --help, -h           Show help.
          --version            Show the installed version.
        """;

    public static CoverScopeCommandLineResult Parse(string[] args, string invocationDirectory)
    {
        string? targetPath = null;
        int? port = null;
        var openBrowser = true;
        var showHelp = false;
        var showVersion = false;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--no-browser":
                    openBrowser = false;
                    break;
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                case "--version":
                    showVersion = true;
                    break;
                case "--port":
                    if (++index >= args.Length)
                        return Failure("Option --port requires a value.");
                    if (!TryParsePort(args[index], out port))
                        return Failure($"Invalid port '{args[index]}'. Use a number from 1 to 65535.");
                    break;
                default:
                    if (argument.StartsWith("--port=", StringComparison.Ordinal))
                    {
                        var value = argument["--port=".Length..];
                        if (!TryParsePort(value, out port))
                            return Failure($"Invalid port '{value}'. Use a number from 1 to 65535.");
                    }
                    else if (argument.StartsWith("-", StringComparison.Ordinal))
                    {
                        return Failure($"Unknown option '{argument}'.");
                    }
                    else if (targetPath is not null)
                    {
                        return Failure("Only one solution or project path may be supplied.");
                    }
                    else
                    {
                        targetPath = argument;
                    }
                    break;
            }
        }

        var fullInvocationDirectory = Path.GetFullPath(invocationDirectory);
        if (targetPath is not null && !showHelp && !showVersion)
        {
            targetPath = Path.GetFullPath(targetPath, fullInvocationDirectory);
            if (!File.Exists(targetPath))
                return Failure($"The solution or project does not exist: {targetPath}");
            if (!Services.SolutionFileBrowser.IsSupportedFile(targetPath))
                return Failure("The target must be a .sln, .slnx, .csproj, .fsproj, or .vbproj file.");
        }

        return new(new(fullInvocationDirectory, targetPath, openBrowser, port, showHelp, showVersion), null);
    }

    public static string GetVersion()
    {
        var version = typeof(CoverScopeCommandLine).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        return string.IsNullOrWhiteSpace(version)
            ? "unknown"
            : version.Split('+', 2)[0];
    }

    private static bool TryParsePort(string value, out int? port)
    {
        if (int.TryParse(value, out var parsed) && parsed is >= 1 and <= 65535)
        {
            port = parsed;
            return true;
        }

        port = null;
        return false;
    }

    private static CoverScopeCommandLineResult Failure(string message) => new(null, message);
}
