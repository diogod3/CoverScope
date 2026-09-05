using Xunit;

namespace DD3.CoverScope.Tests;

public partial class CoverScopeCommandLineTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("not-a-port")]
    public void ShouldRejectInvalidPort(string value)
    {
        var result = Parse("--port", value);

        Assert.False(result.Success);
        Assert.Contains("Invalid port", result.ErrorMessage!);
    }

    [Fact]
    public void ShouldRejectMissingPortValue()
    {
        var result = Parse("--port");

        Assert.False(result.Success);
        Assert.Contains("requires a value", result.ErrorMessage!);
    }

    [Fact]
    public void ShouldRejectUnknownOption()
    {
        var result = Parse("--wat");

        Assert.False(result.Success);
        Assert.Contains("Unknown option", result.ErrorMessage!);
    }

    [Fact]
    public void ShouldRejectMultipleTargets()
    {
        var result = Parse("One.sln", "Two.sln");

        Assert.False(result.Success);
        Assert.Contains("Only one", result.ErrorMessage!);
    }
}
