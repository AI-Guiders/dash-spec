using DashSpec.Abstractions.Viz;
using DashSpec.Host.Components;
using DashSpec.Host.Configuration;
using DashSpec.Host.Plugins;
using DashSpec.Host.Plugins.Builtins;
using DashSpec.Host.Services;
using DashSpec.Host.Services.Abstractions;
using DashSpec.Host.Services.Loading;
using DashSpec.Host.Services.Models;
using DashSpec.Host.Endpoints;
using DashSpec.Host.Services.Dev;
using DashSpec.Host.Services.Presentation;
using DashSpec.Host.Services.Rendering;
using Microsoft.Extensions.Logging.Abstractions;

var builder = WebApplication.CreateBuilder(args);

var dashSpecToml = DashSpecBootstrap.Load(builder.Environment);
var defaultSpecPath = DashSpecBootstrap.ResolveSpecPath(
    builder.Environment.ContentRootPath,
    dashSpecToml.Dashboard.SpecPath);
var startupConfigPath = DashSpecBootstrap.ResolveRuntimeConfigPath(
    defaultSpecPath,
    File.ReadAllText(defaultSpecPath));

builder.Configuration.AddInMemoryCollection(DashSpecTomlLoader.Flatten(dashSpecToml));

builder.Services.AddSingleton(new DashSpecHostContext
{
    StartupRuntimeConfigPath = startupConfigPath,
    DefaultSpecRelativePath = dashSpecToml.Dashboard.SpecPath.Replace('\\', '/'),
    DefaultSpecDirectory = Path.GetDirectoryName(defaultSpecPath)!,
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<DashboardHostOptions>(
    builder.Configuration.GetSection(DashboardHostOptions.SectionName));

var manifest = ConnectorPluginLoader.LoadManifest(dashSpecToml);
ConnectorPluginLoader.RegisterPlugins(
    builder.Services,
    builder.Configuration,
    builder.Environment,
    manifest,
    NullLogger.Instance);

builder.Services.AddSingleton<IVizPlugin, ChartJsVizPlugin>();
builder.Services.AddSingleton<IVizPlugin, CssGridVizPlugin>();
builder.Services.AddSingleton<IVizPlugin, TableHtmlVizPlugin>();
builder.Services.AddSingleton<IVizPlugin, ScalarHtmlVizPlugin>();
builder.Services.AddSingleton<VizPluginRegistry>();

builder.Services.AddScoped<IDashboardSpecLoader, DashboardSpecLoader>();
builder.Services.AddScoped<ICardRenderer, CardRenderService>();
builder.Services.AddScoped<IDashboardSession, DashboardSessionService>();
builder.Services.AddScoped<DashboardPageController>();
builder.Services.AddSingleton<DevSpecReloadNotifier>();
builder.Services.AddHttpClient();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<DevSpecResolveService>();
    builder.Services.AddHostedService<DevSpecFileWatcherService>();
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.UseStaticFiles();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDevEndpoints();

app.Run();
