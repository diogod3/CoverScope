using Xunit;

namespace DD3.CoverScope.Tests;

public partial class CoverScopeCommandLineTests
{
    [Fact]
    public void ShouldDefaultToBrowserAndAvailablePort()
    {
        var result = Parse();

        Assert.True(result.Success);
        Assert.True(result.Options!.OpenBrowser);
        Assert.Null(result.Options.Port);
        Assert.Equal(Path.GetFullPath(directory), result.Options.InvocationDirectory);
    }

    [Fact]
    public void ShouldPreserveRelativeTargetAndParseOptions()
    {
        var result = Parse("Demo.sln", "--no-browser", "--port", "5123");

        Assert.True(result.Success);
        Assert.Equal("Demo.sln", result.Options!.TargetPath);
        Assert.False(result.Options.OpenBrowser);
        Assert.Equal(5123, result.Options.Port);
    }

    [Fact]
    public void ShouldPreserveTargetPathContainingSpaces()
    {
        string path = Path.Combine("Project with spaces", "Demo Tests.csproj");

        var result = Parse(path);

        Assert.True(result.Success);
        Assert.Equal(path, result.Options!.TargetPath);
    }

    [Theory]
    [InlineData("missing.sln")]
    [InlineData("README.md")]
    public void ShouldLeaveTargetValidationToService(string path)
    {
        var result = Parse(path);

        Assert.True(result.Success);
        Assert.Equal(path, result.Options!.TargetPath);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("--version")]
    public void ShouldHandleInformationalOptionsWithoutTargetValidation(string option)
    {
        var result = Parse("missing.sln", option);

        Assert.True(result.Success);
        Assert.Equal(option != "--version", result.Options!.ShowHelp);
        Assert.Equal(option == "--version", result.Options.ShowVersion);
    }

    [Theory]
    [InlineData("--port=1", 1)]
    [InlineData("--port=65535", 65535)]
    public void ShouldAcceptInlinePort(string argument, int expected)
    {
        var result = Parse(argument);

        Assert.True(result.Success);
        Assert.Equal(expected, result.Options!.Port);
    }
}
