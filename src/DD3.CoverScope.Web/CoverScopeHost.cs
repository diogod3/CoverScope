using DD3.CoverScope.Models;
using System.Diagnostics;
using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Brokers.FileSystems;
using DD3.CoverScope.Models.CoverageTargets.Exceptions;
using DD3.CoverScope.Services.Foundations.CoverageTargets;
using DD3.CoverScope.Components;
using DD3.CoverScope.Services;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace DD3.CoverScope;

public record CoverScopeLaunchContext(string InvocationDirectory, string? InitialTargetPath);

internal static class CoverScopeHost
{
    public static Task<int> RunAsync(string[] args) => CoverScopeComposition.CreateCli().ExecuteAsync(args);

    internal static WebApplication CreateApplication(Models.CoverScopeSessionRequest request)
    {
        var options = new CoverScopeCommandLineOptions(request.InvocationDirectory, request.TargetPath, false, request.Port, false, false);
        var applicationDirectory = AppContext.BaseDirectory;
        var packagedWebRoot = Path.Combine(applicationDirectory, "wwwroot");
        var contentRoot = ResolveContentRoot(applicationDirectory, options.InvocationDirectory);
        var webRoot = Directory.Exists(packagedWebRoot)
            ? packagedWebRoot
            : Path.Combine(contentRoot, "wwwroot");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [],
            ApplicationName = typeof(CoverScopeHost).Assembly.GetName().Name,
            ContentRootPath = contentRoot,
            WebRootPath = webRoot
        });

        builder.WebHost.UseUrls($"http://127.0.0.1:{options.Port?.ToString() ?? "0"}");
        builder.Services
            .AddRazorComponents()
            .AddInteractiveServerComponents();
        builder.Services.AddSingleton(new CoverScopeLaunchContext(options.InvocationDirectory, options.TargetPath));
        builder.Services.AddSingleton<StartupCoverageCoordinator>();
        CoverScopeComposition.AddBackend(builder.Services, options.InvocationDirectory);
        var app = builder.Build();
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            app.UseHsts();
        }

        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = context =>
                context.Context.Response.Headers["Cache-Control"] = "no-cache, no-store"
        });
        app.UseAntiforgery();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        return app;
    }

    private static string ResolveContentRoot(string applicationDirectory, string invocationDirectory)
    {
        if (Directory.Exists(Path.Combine(applicationDirectory, "wwwroot")))
            return applicationDirectory;

        if (Directory.Exists(Path.Combine(invocationDirectory, "wwwroot")))
            return invocationDirectory;

        var repositoryProject = Path.Combine(invocationDirectory, "src", "DD3.CoverScope.Web");
        return Directory.Exists(Path.Combine(repositoryProject, "wwwroot"))
            ? repositoryProject
            : applicationDirectory;
    }

}
