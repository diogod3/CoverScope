using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using DD3.CoverScope.Models;

namespace DD3.CoverScope.Components.Shared;
public enum WorkspaceMode { Explorer, Metrics }
public partial class CoverageCommandBar
{
    [Parameter] public string TargetPath { get; set; } = string.Empty;
    [Parameter] public bool IsBusy { get; set; }
    [Parameter] public bool IsPickingSolution { get; set; }
    [Parameter] public CoverageReport? Report { get; set; }
    [Parameter] public WorkspaceMode Mode { get; set; }
    [Parameter] public CoverageRunOutcome? LastRunOutcome { get; set; }
    [Parameter] public TestRunSummary? Tests { get; set; }
    [Parameter] public EventCallback OnShowRunSetup { get; set; }
    [Parameter] public EventCallback OnShowMetrics { get; set; }
    [Parameter] public EventCallback OnShowExplorer { get; set; }
    [Parameter] public EventCallback OnShowImportSetup { get; set; }
    [Parameter] public EventCallback OnRunCoverage { get; set; }
}
