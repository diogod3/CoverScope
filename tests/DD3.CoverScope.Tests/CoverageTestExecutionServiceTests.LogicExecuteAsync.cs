using Xunit;
namespace DD3.CoverScope.Tests;
public partial class CoverageTestExecutionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_PreservesArgumentBoundariesAndProcessResult()
    {
        var result = await CreateService().ExecuteAsync(Absolute("Sample project.csproj"), Absolute("results"), Absolute("settings.xml"));
        Assert.Equal(1, result.ExitCode);
        Assert.Equal("failed test", result.Output);
        Assert.Equal("dotnet", broker.StartInfo?.FileName);
        Assert.False(broker.StartInfo?.UseShellExecute);
        Assert.Equal(new[] { "test", Absolute("Sample project.csproj"), "--collect:XPlat Code Coverage",
            "--results-directory", Absolute("results"), "--logger", "trx;LogFilePrefix=coverscope",
            "--settings", Absolute("settings.xml") }, broker.StartInfo!.ArgumentList);
    }
}
