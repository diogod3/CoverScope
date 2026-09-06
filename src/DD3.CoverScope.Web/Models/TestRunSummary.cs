namespace DD3.CoverScope.Models;

public record TestRunSummary(
    int Passed,
    int Failed,
    int Skipped,
    int Total,
    TimeSpan Duration,
    IReadOnlyList<TestFailure> Failures);
