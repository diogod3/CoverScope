using System.Globalization;
using DD3.CoverScope.Models;

namespace DD3.CoverScope.Services;

public static class CoverageRunLabelFormatter
{
    public static string Format(
        CoverageRun run,
        CultureInfo? culture = null,
        TimeZoneInfo? timeZone = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        timeZone ??= TimeZoneInfo.Local;
        var localTime = TimeZoneInfo.ConvertTime(run.StartedAt, timeZone);
        var date = localTime.ToString("MMM d", culture);
        var time = localTime.ToString("t", culture);
        return $"{run.Target.Name} — {date}, {time}";
    }

    public static string StatusLabel(CoverageRunStatus status) => status switch
    {
        CoverageRunStatus.Created => "Created",
        CoverageRunStatus.Running => "Running",
        CoverageRunStatus.CancellationRequested => "Cancelling",
        CoverageRunStatus.Succeeded => "Completed",
        CoverageRunStatus.TestsFailed => "Failed tests",
        CoverageRunStatus.ExecutionFailed => "Failed",
        CoverageRunStatus.Cancelled => "Cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}
