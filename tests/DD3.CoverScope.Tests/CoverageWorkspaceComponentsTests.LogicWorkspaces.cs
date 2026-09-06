using DD3.CoverScope;
using DD3.CoverScope.Components.Shared;
using DD3.CoverScope.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Xunit;

namespace DD3.CoverScope.Tests;
public partial class CoverageWorkspaceComponentsTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Workspaces_RenderWithMutuallyExclusiveVisibility(bool explorerVisible)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new CoverScopeLaunchContext(Path.GetTempPath(), null));
        services.AddSingleton<IJSRuntime>(new NullJsRuntime());
        CoverScopeComposition.AddBackend(services, Path.GetTempPath());
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        await using var scope = provider.CreateAsyncScope();
        await using var renderer = new HtmlRenderer(scope.ServiceProvider, provider.GetRequiredService<ILoggerFactory>());
        var report = new CoverageReport("Sample", DateTimeOffset.UtcNow, "sample.xml", [], []);
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var explorer = await renderer.RenderComponentAsync<CoverageExplorerWorkspace>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(CoverageExplorerWorkspace.Report)] = report,
                    [nameof(CoverageExplorerWorkspace.IsVisible)] = explorerVisible
                }));
            var metrics = await renderer.RenderComponentAsync<CoverageMetricsWorkspace>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(CoverageMetricsWorkspace.Report)] = report,
                    [nameof(CoverageMetricsWorkspace.IsVisible)] = !explorerVisible
                }));
            Assert.Equal(!explorerVisible, explorer.ToHtmlString().Contains("hidden"));
            Assert.Equal(explorerVisible, metrics.ToHtmlString().Contains("hidden"));
        });
    }
}
