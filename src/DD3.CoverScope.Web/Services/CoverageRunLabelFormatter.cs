using System.Globalization;
using DD3.CoverScope.Models;

namespace DD3.CoverScope.Services;

public static class CoverageRunLabelFormatter
{
    public static string Format(
        CoverageRunManifest run,
        CultureInfo? culture = null,
        TimeZoneInfo? timeZone = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        timeZone ??= TimeZoneInfo.Local;
        var localTime = TimeZoneInfo.ConvertTime(run.StartedAtUtc, timeZone);
        var date = localTime.ToString("MMM d", culture);
        var time = localTime.ToString("t", culture);
        return $"{run.Target.Name} — {date}, {time}";
    }

    public static string StatusLabel(CoverageRunStatus status) => status switch
    {
        CoverageRunStatus.InProgress => "Incomplete",
        CoverageRunStatus.Completed => "Completed",
        CoverageRunStatus.CompletedWithTestFailures => "Failed tests",
        CoverageRunStatus.Failed => "Failed",
        CoverageRunStatus.Cancelled => "Cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}
