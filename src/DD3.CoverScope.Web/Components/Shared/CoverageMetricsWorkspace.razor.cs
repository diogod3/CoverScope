using System.Globalization;
using DD3.CoverScope.Models;
using DD3.CoverScope.Models.Exceptions;
using DD3.CoverScope.Services;
using DD3.CoverScope.Services.Views;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
namespace DD3.CoverScope.Components.Shared;
public partial class CoverageMetricsWorkspace
{
    [Inject] public ICoverageMetricsWorkspaceViewService ViewService { get; set; } = default!;


    [Parameter, EditorRequired] public CoverageReport Report { get; set; } = default!;
    [Parameter] public bool IsVisible { get; set; } = true;
    [Parameter] public EventCallback<CoverageMetricRow> OnOpenClass { get; set; }

    private CoverageReport? currentReport;
    private PackageCoverage? selectedProject;
    private NamespaceCoverage? selectedNamespace;
    private MetricSort sortMode = MetricSort.MostMissed;
    private string search = string.Empty;
    private int coverageThreshold = 101;
    private int minimumLines;
    private bool onlyWithGaps;
    private IReadOnlyList<CoverageMetricRow> baseRows = [];
    private IReadOnlyList<CoverageMetricRow> visibleRows = [];

    private string Search
    {
        get => search;
        set
        {
            if (search == value) return;
            search = value;
            RefreshVisibleRows();
        }
    }

    private MetricSort SortMode
    {
        get => sortMode;
        set
        {
            if (sortMode == value) return;
            sortMode = value;
            RefreshVisibleRows();
        }
    }

    private int CoverageThreshold
    {
        get => coverageThreshold;
        set
        {
            if (coverageThreshold == value) return;
            coverageThreshold = value;
            RefreshVisibleRows();
        }
    }

    private int MinimumLines
    {
        get => minimumLines;
        set
        {
            if (minimumLines == value) return;
            minimumLines = value;
            RefreshVisibleRows();
        }
    }

    private bool OnlyWithGaps
    {
        get => onlyWithGaps;
        set
        {
            if (onlyWithGaps == value) return;
            onlyWithGaps = value;
            RefreshVisibleRows();
        }
    }

    private string LevelName => selectedNamespace is not null ? "Classes" : selectedProject is not null ? "Namespaces" : "Projects";
    private string LevelTitle => $"Coverage by {LevelName.ToLowerInvariant()}";
    private string LevelDescription => selectedNamespace is not null
        ? "Logical classes are aggregated across all of their partial source files."
        : selectedProject is not null
            ? "Compare the namespaces in this project and drill into their classes."
            : "Compare projects and start with the areas containing the most missed lines.";

    private IReadOnlyList<CoverageMetricRow> VisibleRows => visibleRows;

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(currentReport, Report)) return;
        currentReport = Report;
        selectedProject = null;
        selectedNamespace = null;
        search = string.Empty;
        RefreshRows();
    }

    private IReadOnlyList<CoverageMetricRow> BuildRows() =>
        ViewService.GetMetrics(Report, selectedProject, selectedNamespace);

    private void RefreshRows()
    {
        baseRows = BuildRows();
        RefreshVisibleRows();
    }

    private void RefreshVisibleRows() =>
        visibleRows = ViewService.Filter(baseRows, search, minimumLines, onlyWithGaps, coverageThreshold, sortMode).ToArray();



    private Task OpenRow(CoverageMetricRow row)
    {
        if (row.Scope == CoverageMetricScope.Project)
        {
            selectedProject = row.Project;
            selectedNamespace = null;
            search = string.Empty;
            RefreshRows();
            return Task.CompletedTask;
        }
        if (row.Scope == CoverageMetricScope.Namespace)
        {
            selectedProject = row.Project;
            selectedNamespace = row.Namespace;
            search = string.Empty;
            RefreshRows();
            return Task.CompletedTask;
        }
        return OnOpenClass.InvokeAsync(row);
    }

    private Task HandleRowKey(KeyboardEventArgs args, CoverageMetricRow row) =>
        args.Key is "Enter" or " " ? OpenRow(row) : Task.CompletedTask;

    private void ShowProjects()
    {
        selectedProject = null;
        selectedNamespace = null;
        search = string.Empty;
        RefreshRows();
    }

    private void ShowNamespaces()
    {
        selectedNamespace = null;
        search = string.Empty;
        RefreshRows();
    }

    private static string ScopeIcon(CoverageMetricScope scope) => scope switch
    {
        CoverageMetricScope.Project => "P",
        CoverageMetricScope.Namespace => "N",
        _ => "C"
    };

    private static string RowSubtitle(CoverageMetricRow row) => row.Scope switch
    {
        CoverageMetricScope.Project => $"{row.ChildCount} namespaces · {row.FileCount} files",
        CoverageMetricScope.Namespace => $"{row.ChildCount} classes · {row.FileCount} files",
        _ when row.FileCount > 1 => $"partial class · {row.ChildCount} methods across {row.FileCount} files",
        _ => $"{row.ChildCount} methods · {row.ClassFragments.FirstOrDefault()?.RelativePath}"
    };

    private static string Ratio(CoverageMetric metric) => metric.Total == 0
        ? "No data"
        : $"{Format(metric.Covered)} / {Format(metric.Total)}";

    private static string CompactMetric(CoverageMetric metric) => metric.Total == 0
        ? "—"
        : $"{Format(metric.Covered)} / {Format(metric.Total)} · {metric.Percent:0.#}%";

    private static string BarStyle(CoverageMetric metric) =>
        $"--covered:{Math.Clamp(metric.Percent, 0, 100).ToString("0.##", CultureInfo.InvariantCulture)}%;";

    private static string Format(int value) => value.ToString("N0", CultureInfo.CurrentCulture);

}
