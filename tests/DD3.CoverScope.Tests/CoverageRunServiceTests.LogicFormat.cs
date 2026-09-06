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
    public void Format_UsesTargetNameAndRequestedLocalTimeWithoutStatus()
    {
        var run = CreateManifest(CoverageRunStatus.TestsFailed);
        var timeZone = TimeZoneInfo.CreateCustomTimeZone("UTC plus one", TimeSpan.FromHours(1), "UTC plus one", "UTC plus one");

        var label = CoverageRunLabelFormatter.Format(run, CultureInfo.GetCultureInfo("en-GB"), timeZone);

        Assert.StartsWith("Sample — ", label, StringComparison.Ordinal);
        Assert.EndsWith(", 15:32", label, StringComparison.Ordinal);
        Assert.DoesNotContain("failed", label, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Failed tests", CoverageRunLabelFormatter.StatusLabel(run.Status));
    }
}
