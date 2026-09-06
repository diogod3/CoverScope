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
public partial class CoveragePill
{

[Parameter, EditorRequired] public CoverageMetric Metric { get; set; } = new(0, 0);
    private string Band => Metric.Total == 0 ? "empty" : Metric.Percent switch
    {
        >= 80 => "good",
        >= 60 => "medium",
        _ => "poor"
    };

}
