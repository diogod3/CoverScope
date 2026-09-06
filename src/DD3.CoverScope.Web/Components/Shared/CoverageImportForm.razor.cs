using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using DD3.CoverScope.Models;

namespace DD3.CoverScope.Components.Shared;
public partial class CoverageImportForm
{
    [Parameter] public string ReportPath { get; set; } = string.Empty;
    [Parameter] public EventCallback<string> ReportPathChanged { get; set; }
    [Parameter] public string SourceRoot { get; set; } = string.Empty;
    [Parameter] public EventCallback<string> SourceRootChanged { get; set; }
    [Parameter] public EventCallback<InputFileChangeEventArgs> OnImportFile { get; set; }
    [Parameter] public EventCallback OnOpenReport { get; set; }
}
