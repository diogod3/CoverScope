namespace DD3.CoverScope.Models;

public record CoverageRunResult(
    CoverageRunOutcome Outcome,
    string Message,
    string Output,
    string? ReportPath = null,
    TestRunSummary? Tests = null,
    CoverageRun? Run = null)
{
    public bool Success => Outcome == CoverageRunOutcome.Succeeded;
    public bool HasCoverage => ReportPath is not null;
}
