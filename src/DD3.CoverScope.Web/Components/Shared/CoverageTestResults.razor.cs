using DD3.CoverScope.Models;
using Microsoft.AspNetCore.Components;
namespace DD3.CoverScope.Components.Shared;
public partial class CoverageTestResults
{
    [Parameter, EditorRequired] public TestRunSummary Tests { get; set; } = default!;
    [Parameter] public string? StatusMessage { get; set; }
    [Parameter] public string? Output { get; set; }
    [Parameter] public Func<TestFailure, bool> CanOpenSource { get; set; } = _ => false;
    [Parameter] public EventCallback<TestFailure> OnCopyFailure { get; set; }
    [Parameter] public EventCallback<TestFailure> OnOpenFailureSource { get; set; }
    private static string FailureHeading(int count) => count == 1 ? "1 test failed" : $"{count} tests failed";
    private static string AssemblyLabel(string assembly) =>
        string.IsNullOrWhiteSpace(assembly) ? "Test assembly" : assembly;
    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes >= 1) return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
        if (duration.TotalSeconds >= 1) return $"{duration.TotalSeconds:0.##}s";
        return $"{Math.Max(0, duration.TotalMilliseconds):0}ms";
    }

}
