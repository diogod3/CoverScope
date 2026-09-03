using System.Diagnostics;
using DD3.CoverScope.Components;
using DD3.CoverScope.Services;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace DD3.CoverScope;

public sealed record CoverScopeLaunchContext(string InvocationDirectory, string? InitialTargetPath);

internal static class CoverScopeHost
{
    public static async Task<int> RunAsync(string[] args)
    {
        var invocationDirectory = Environment.CurrentDirectory;
        CoverScopeCommandLineResult parseResult;
        try
        {
            parseResult = CoverScopeCommandLine.Parse(args, invocationDirectory);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            Console.Error.WriteLine($"coverscope: {ex.Message}");
            return 2;
        }

        if (!parseResult.Success)
        {
            Console.Error.WriteLine($"coverscope: {parseResult.ErrorMessage}");
            Console.Error.WriteLine("Run 'coverscope --help' for usage.");
            return 2;
        }

        var options = parseResult.Options!;
        if (options.ShowHelp)
        {
            Console.WriteLine(CoverScopeCommandLine.HelpText);
            return 0;
        }

        if (options.ShowVersion)
        {
            Console.WriteLine(CoverScopeCommandLine.GetVersion());
            return 0;
        }

        var applicationDirectory = AppContext.BaseDirectory;
        var packagedWebRoot = Path.Combine(applicationDirectory, "wwwroot");
        var contentRoot = ResolveContentRoot(applicationDirectory, invocationDirectory);
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
        builder.Services.AddSingleton(new SolutionFileBrowser(options.InvocationDirectory));
        builder.Services.AddSingleton<CoberturaParser>();
        builder.Services.AddSingleton<CoberturaReportMerger>();
        builder.Services.AddSingleton<CoverletRunSettingsWriter>();
        builder.Services.AddSingleton<CoverageSettingsStore>();
        builder.Services.AddSingleton<TrxTestResultParser>();
        builder.Services.AddSingleton<CoverageRunner>();
        builder.Services.AddSingleton<CoverageMetricsBuilder>();
        builder.Services.AddScoped<CoverageWorkspaceProjectionCache>();

        await using var app = builder.Build();

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

        try
        {
            await app.StartAsync();
            var url = ResolveBoundUrl(app);
            Console.WriteLine($"CoverScope is running at {url}");
            Console.WriteLine("Press Ctrl+C to stop.");

            if (options.OpenBrowser)
                TryOpenBrowser(url);

            await app.WaitForShutdownAsync();
            return 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            Console.Error.WriteLine($"coverscope: failed to start: {ex.Message}");
            return 1;
        }
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

    private static string ResolveBoundUrl(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
        return addresses?.SingleOrDefault(address => address.StartsWith("http://127.0.0.1:", StringComparison.OrdinalIgnoreCase))
            ?? app.Urls.Single();
    }

    private static void TryOpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Console.Error.WriteLine($"CoverScope could not open the browser automatically: {ex.Message}");
        }
    }
}
