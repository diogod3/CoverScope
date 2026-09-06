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
    [Theory]
    [InlineData(CoverageRunOutcome.Succeeded, true, CoverageRunStatus.Succeeded)]
    [InlineData(CoverageRunOutcome.TestsFailed, true, CoverageRunStatus.TestsFailed)]
    [InlineData(CoverageRunOutcome.TestsFailed, false, CoverageRunStatus.ExecutionFailed)]
    [InlineData(CoverageRunOutcome.ExecutionFailed, false, CoverageRunStatus.ExecutionFailed)]
    [InlineData(CoverageRunOutcome.ExecutionFailed, true, CoverageRunStatus.ExecutionFailed)]
    [InlineData(CoverageRunOutcome.Cancelled, false, CoverageRunStatus.Cancelled)]
    public void ToStatus_DistinguishesCoverageAndCollectionOutcomes(
        CoverageRunOutcome outcome,
        bool hasCoverage,
        CoverageRunStatus expected)
    {
        Assert.Equal(expected, CoverageRunService.ToStatus(outcome, hasCoverage));
    }
}
