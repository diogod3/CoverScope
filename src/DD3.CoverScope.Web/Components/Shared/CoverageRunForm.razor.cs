using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using DD3.CoverScope.Models;

namespace DD3.CoverScope.Components.Shared;
public partial class CoverageRunForm
{
    [Parameter] public string TargetPath { get; set; } = string.Empty;
    [Parameter] public EventCallback<string> TargetPathChanged { get; set; }
    [Parameter, EditorRequired] public CoverageSettings Settings { get; set; } = default!;
    [Parameter] public bool IsBusy { get; set; }
    [Parameter] public bool IsPickingSolution { get; set; }
    [Parameter] public EventCallback OnChooseSolution { get; set; }
    [Parameter] public EventCallback OnRunCoverage { get; set; }
}
