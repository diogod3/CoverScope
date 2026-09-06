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
public partial class MetricRing
{

[Parameter, EditorRequired] public CoverageMetric Metric { get; set; } = new(0, 0);
    [Parameter, EditorRequired] public string Label { get; set; } = string.Empty;
    [Parameter] public string Color { get; set; } = "#7c6cff";
    private double BoundedPercent => Math.Clamp(Metric.Percent, 0, 100);

}
