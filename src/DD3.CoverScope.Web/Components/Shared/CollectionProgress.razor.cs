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
public partial class CollectionProgress : IAsyncDisposable
{

[Parameter, EditorRequired]
    public CoverageCollectionPhase Phase { get; set; }

    [Parameter, EditorRequired]
    public DateTimeOffset StartedAt { get; set; }

    [Parameter, EditorRequired]
    public string TargetPath { get; set; } = string.Empty;

    private readonly CancellationTokenSource timerCancellation = new();
    private Task? timerTask;

    private string ElapsedLabel
    {
        get
        {
            var elapsed = DateTimeOffset.UtcNow - StartedAt;
            return elapsed.TotalMinutes >= 1
                ? $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds:00}s"
                : $"{Math.Max(0, (int)elapsed.TotalSeconds)}s";
        }
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
            timerTask = UpdateElapsedAsync(timerCancellation.Token);
    }

    private async Task UpdateElapsedAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
                await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        await timerCancellation.CancelAsync();
        if (timerTask is not null)
            await timerTask;
        timerCancellation.Dispose();
    }

}
