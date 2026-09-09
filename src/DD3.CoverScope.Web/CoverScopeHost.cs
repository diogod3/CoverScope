using System.Diagnostics;
using DD3.CoverScope.Components;
using DD3.CoverScope.Services;
using DD3.CoverScope.Brokers;
using DD3.CoverScope.Services.Reviews;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace DD3.CoverScope;

public sealed record CoverScopeLaunchContext(string InvocationDirectory, string? InitialTargetPath);

internal sealed record CoverScopeStartupUrls(string Preferred, string Fallback);

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
        builder.Services.AddSingleton<IFileSystemBroker, FileSystemBroker>();
        builder.Services.AddSingleton<IProcessBroker, ProcessBroker>();
        builder.Services.AddSingleton<GitComparisonService>();
        builder.Services.AddSingleton<ReviewStore>();
        builder.Services.AddSingleton<ReportReader>();
        builder.Services.AddSingleton<VerificationService>();
        builder.Services.AddSingleton<CodeComparisonService>();
        builder.Services.AddSingleton<ReviewOrchestrationService>();
        builder.Services.AddSingleton<ReviewExportService>();

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
            var urls = CreateStartupUrls(ResolveBoundUrl(app));
            AnnounceStartup(urls, options.OpenBrowser, OpenBrowser, Console.Out, Console.Error);

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

    internal static CoverScopeStartupUrls CreateStartupUrls(string boundUrl)
    {
        var fallback = new Uri(boundUrl).GetLeftPart(UriPartial.Authority);
        var preferred = new UriBuilder(fallback)
        {
            Host = "coverscope.localhost"
        }.Uri.GetLeftPart(UriPartial.Authority);

        return new(preferred, fallback);
    }

    internal static void AnnounceStartup(
        CoverScopeStartupUrls urls,
        bool openBrowser,
        Action<string> browserLauncher,
        TextWriter output,
        TextWriter error)
    {
        output.WriteLine($"CoverScope is running at {urls.Preferred}");
        output.WriteLine($"Loopback fallback: {urls.Fallback}");
        output.WriteLine("Press Ctrl+C to stop.");

        if (!openBrowser)
            return;

        try
        {
            browserLauncher(urls.Preferred);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            error.WriteLine($"CoverScope could not open the browser automatically: {ex.Message}");
        }
    }

    private static void OpenBrowser(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
