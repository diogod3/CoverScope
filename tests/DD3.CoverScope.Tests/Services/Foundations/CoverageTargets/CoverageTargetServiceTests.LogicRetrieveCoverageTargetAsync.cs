using DD3.CoverScope.Models.CoverageTargets;
using Xunit;

namespace DD3.CoverScope.Tests.Services.Foundations.CoverageTargets;

public partial class CoverageTargetServiceTests
{
    [Theory]
    [InlineData(".sln", CoverageTargetType.Solution)]
    [InlineData(".slnx", CoverageTargetType.Solution)]
    [InlineData(".csproj", CoverageTargetType.Project)]
    [InlineData(".fsproj", CoverageTargetType.Project)]
    [InlineData(".vbproj", CoverageTargetType.Project)]
    [InlineData(".SLN", CoverageTargetType.Solution)]
    [InlineData(".CSPROJ", CoverageTargetType.Project)]
    public async Task ShouldRetrieveTargetUsingExplicitBaseDirectoryAsync(
        string extension,
        CoverageTargetType expectedType)
    {
        string name = $"Project {Guid.NewGuid():N}";
        string relativePath = Path.Combine("folder with spaces", name + extension);
        string expectedPath = Path.GetFullPath(relativePath, baseDirectory);

        var target = await service.RetrieveCoverageTargetAsync(relativePath, baseDirectory);

        Assert.Equal(name, target.Name);
        Assert.Equal(expectedPath, target.Path);
        Assert.Equal(expectedType, target.Type);
        Assert.Equal(new[] { expectedPath }, fileSystemBroker.RequestedPaths);
        Assert.Equal(new[] { "trace", "filesystem", "completed" }, events);
        Assert.Equal(
            "CoverageTargetService.RetrieveCoverageTargetAsync",
            Assert.Single(diagnosticsBroker.ActivityNames));
    }

    [Fact]
    public async Task ShouldPreserveAbsoluteTargetPathAsync()
    {
        string absolutePath = Path.Combine(baseDirectory, "Other", $"{Guid.NewGuid():N}.sln");
        string otherBase = Path.Combine(baseDirectory, "Invocation");

        var target = await service.RetrieveCoverageTargetAsync(absolutePath, otherBase);

        Assert.Equal(absolutePath, target.Path);
        Assert.Equal(absolutePath, Assert.Single(fileSystemBroker.RequestedPaths));
    }

    [Fact]
    public async Task ShouldResolveParentSegmentsBeforeAccessingFilesystemAsync()
    {
        string relativePath = Path.Combine("nested", "..", "Demo.slnx");

        var target = await service.RetrieveCoverageTargetAsync(relativePath, baseDirectory);

        Assert.Equal(Path.Combine(baseDirectory, "Demo.slnx"), target.Path);
        Assert.Equal(target.Path, Assert.Single(fileSystemBroker.RequestedPaths));
    }
}
