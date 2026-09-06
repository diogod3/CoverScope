using DD3.CoverScope.Components.Shared;
using DD3.CoverScope.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DD3.CoverScope.Tests;

public partial class CollectionProgressComponentTests
{
    [Fact]
    public async Task Render_ExposesPhaseTargetElapsedTimeAndAccessibleStatus()
    {
        await using var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            services,
            services.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(CollectionProgress.Phase)] = CoverageCollectionPhase.RunningTests,
                [nameof(CollectionProgress.StartedAt)] = DateTimeOffset.UtcNow.AddSeconds(-4),
                [nameof(CollectionProgress.TargetPath)] = Path.Combine("repo", "My App.sln")
            });
            var output = await renderer.RenderComponentAsync<CollectionProgress>(parameters);
            return output.ToHtmlString();
        });

        Assert.Contains("Collecting coverage", html, StringComparison.Ordinal);
        Assert.Contains("My App.sln", html, StringComparison.Ordinal);
        Assert.Contains("Running tests", html, StringComparison.Ordinal);
        Assert.Contains("elapsed", html, StringComparison.Ordinal);
        Assert.Contains("role=\"status\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-live=\"polite\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-atomic=\"true\"", html, StringComparison.Ordinal);
    }
}
