using DD3.CoverScope;
using Xunit;

namespace DD3.CoverScope.Tests;

public sealed class CoverScopeCommandLineTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"coverscope-cli-{Guid.NewGuid():N}");

    public CoverScopeCommandLineTests() => Directory.CreateDirectory(directory);

    [Fact]
    public void Parse_DefaultsToBrowserAndAvailablePort()
    {
        var result = CoverScopeCommandLine.Parse([], directory);

        Assert.True(result.Success);
        Assert.True(result.Options!.OpenBrowser);
        Assert.Null(result.Options.Port);
        Assert.Equal(Path.GetFullPath(directory), result.Options.InvocationDirectory);
    }

    [Fact]
    public void Parse_AcceptsRelativeTargetNoBrowserAndPort()
    {
        var target = Path.Combine(directory, "Demo.sln");
        File.WriteAllText(target, string.Empty);

        var result = CoverScopeCommandLine.Parse(["Demo.sln", "--no-browser", "--port", "5123"], directory);

        Assert.True(result.Success);
        Assert.Equal(target, result.Options!.TargetPath);
        Assert.False(result.Options.OpenBrowser);
        Assert.Equal(5123, result.Options.Port);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("not-a-port")]
    public void Parse_RejectsInvalidPort(string value)
    {
        var result = CoverScopeCommandLine.Parse(["--port", value], directory);

        Assert.False(result.Success);
        Assert.Contains("Invalid port", result.ErrorMessage!);
    }

    [Fact]
    public void Parse_RejectsUnknownOption()
    {
        var result = CoverScopeCommandLine.Parse(["--wat"], directory);

        Assert.False(result.Success);
        Assert.Contains("Unknown option", result.ErrorMessage!);
    }

    [Fact]
    public void Parse_HelpDoesNotValidateTarget()
    {
        var result = CoverScopeCommandLine.Parse(["missing.sln", "--help"], directory);

        Assert.True(result.Success);
        Assert.True(result.Options!.ShowHelp);
    }

    public void Dispose()
    {
        try { Directory.Delete(directory, recursive: true); }
        catch (IOException) { }
    }
}
