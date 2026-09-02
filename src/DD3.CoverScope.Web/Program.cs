using DD3.CoverScope.Components;
using DD3.CoverScope.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSingleton<CoberturaParser>();
builder.Services.AddSingleton<CoberturaReportMerger>();
builder.Services.AddSingleton<CoverletRunSettingsWriter>();
builder.Services.AddSingleton<CoverageSettingsStore>();
builder.Services.AddSingleton<TrxTestResultParser>();
builder.Services.AddSingleton<CoverageRunner>();
builder.Services.AddSingleton<CoverageMetricsBuilder>();
builder.Services.AddSingleton<SolutionFileBrowser>();

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

app.Run();
