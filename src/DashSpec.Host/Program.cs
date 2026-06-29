using DashSpec.Host.Components;
using DashSpec.Host.Configuration;
using DashSpec.Host.Plugins;
using DashSpec.Host.Services;
using DashSpec.Host.Services.Loading;
using DashSpec.Host.Services.Models;
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

builder.Services.AddScoped<DashboardSpecLoader>();
builder.Services.AddScoped<CardRenderService>();
builder.Services.AddScoped<DashboardSessionService>();

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

app.Run();
