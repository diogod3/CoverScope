using DD3.CoverScope.Services.Foundations.CoverageRuns;
using DD3.CoverScope.Brokers.Identifiers;
using DD3.CoverScope.Models.CoverageTargets;
using DD3.CoverScope.Models.Exceptions;
using System.Globalization;
using DD3.CoverScope;
using DD3.CoverScope.Models;
using DD3.CoverScope.Services;
using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Brokers.FileSystems;
using DD3.CoverScope.Services.Foundations.CoverageTargets;
using Xunit;

namespace DD3.CoverScope.Tests;

public partial class CoverageRunServiceTests
{
    [Fact]
    public async Task ResolveTargetRoot_MatchesCommandLineTargetResolution()
    {
        var targetPath = CreateTarget("Sample.sln");
        var parsed = CoverScopeCommandLine.Parse(["Sample.sln"], directory);

        Assert.True(parsed.Success);
        var targetService = new CoverageTargetService(new FileSystemBroker(), new DiagnosticsBroker());
        var target = await targetService.RetrieveCoverageTargetAsync(
            parsed.Options!.TargetPath!, parsed.Options.InvocationDirectory);

        Assert.Equal(directory, CoverageRunService.ResolveTargetRoot(target.Path));
    }
}
