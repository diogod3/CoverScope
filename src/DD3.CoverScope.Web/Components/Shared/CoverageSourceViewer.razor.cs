using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using DD3.CoverScope.Models;

namespace DD3.CoverScope.Components.Shared;
public partial class CoverageSourceViewer
{
    [Parameter] public FileCoverage? File { get; set; }
    [Parameter] public ClassCoverage? Class { get; set; }
    [Parameter] public MethodCoverage? Method { get; set; }
    [Parameter] public IReadOnlyList<SourceLine> Lines { get; set; } = [];
    [Parameter] public int TargetLine { get; set; }
}
