using System.Globalization;
using DD3.CoverScope.Models;
using DD3.CoverScope.Models.Exceptions;
using DD3.CoverScope.Services;
using DD3.CoverScope.Services.Views;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;

namespace DD3.CoverScope.Components.Shared;
public partial class SolutionBrowser
{
    [Inject] public ISolutionBrowserViewService ViewService { get; set; } = default!;
    [Parameter] public string? CurrentSelection { get; set; }
    [Parameter] public EventCallback<string> OnSelected { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            browserLocations = ViewService.GetLocations();
            ApplyBrowserSnapshot(await Task.Run(() => ViewService.Open(CurrentSelection)));
        }
        catch (CoverageOperationException exception) { browserError = exception.Message; }
    }
    private Task CloseSolutionBrowser() => OnClose.InvokeAsync();

    private string browserPathInput = string.Empty;
    private string? browserParent;
    private string? browserError;
    private IReadOnlyList<SolutionBrowserLocation> browserLocations = [];
    private IReadOnlyList<SolutionBrowserEntry> browserEntries = [];
    private async Task BrowseTo(string path)
    {
        try { ApplyBrowserSnapshot(await Task.Run(() => ViewService.Browse(path))); }
        catch (CoverageOperationException exception) { browserError = exception.Message; }
    }
    private Task BrowseToTypedPath() => BrowseTo(browserPathInput);
    private Task BrowseUp() => browserParent is null ? Task.CompletedTask : BrowseTo(browserParent);
    private async Task ActivateBrowserEntry(SolutionBrowserEntry entry)
    {
        if (entry.IsDirectory)
        {
            await BrowseTo(entry.Path);
            return;
        }

        var result = await ViewService.ValidateSelectionAsync(entry.Path);
        if (!result.Success || result.SelectedPath is null)
        {
            browserError = result.Message;
            return;
        }

        await OnSelected.InvokeAsync(result.SelectedPath);
        await OnClose.InvokeAsync();
    }
    private void ApplyBrowserSnapshot(SolutionBrowserSnapshot snapshot)
    {
        browserPathInput = snapshot.DirectoryPath;
        browserParent = snapshot.ParentPath;
        browserEntries = snapshot.Entries;
        browserError = snapshot.ErrorMessage;
    }

}
