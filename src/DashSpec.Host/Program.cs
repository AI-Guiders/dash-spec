using DashSpec.Core.Parsing;
using DashSpec.Host.Components;
using DashSpec.Host.Configuration;
using DashSpec.Host.Endpoints;
using DashSpec.Host.Middleware;
using DashSpec.Host.Plugins;
using DashSpec.Host.Security;
using DashSpec.Host.Services;
using DashSpec.Host.Services.Abstractions;
using DashSpec.Host.Services.Dev;
using DashSpec.Host.Services.Diagnostics;
using DashSpec.Host.Services.Loading;
using DashSpec.Host.Services.Presentation;
using DashSpec.Host.Services.Rendering;
using Microsoft.Extensions.Logging.Abstractions;

var builder = WebApplication.CreateBuilder(args);

var bootstrap = DashSpecBootstrap.LoadBootstrap(builder.Environment);
var accessOptions = new DashSpecAccessOptions { ApiKey = bootstrap.Access.ApiKey };
var envKey = Environment.GetEnvironmentVariable("DASHSPEC_API_KEY");
if (!string.IsNullOrWhiteSpace(envKey))
{
    accessOptions.ApiKey = envKey;
}

var catalog = DashSpecBootstrap.LoadCatalog(bootstrap, builder.Environment.ContentRootPath);
var defaultSpecPath = DashSpecBootstrap.ResolveActiveSpecFullPath(catalog);
var dashSpecToml = DashSpecBootstrap.Load(builder.Environment);
var defaultSpecText = File.ReadAllText(defaultSpecPath);
var startupConfigPath = DashSpecBootstrap.ResolveRuntimeConfigPath(
    defaultSpecPath,
    defaultSpecText);
var startupRuntimeReference = DashSpecParser.ReadRuntimePath(defaultSpecText)
    ?? throw new InvalidOperationException("Default catalog entry .dashspec must declare @runtime.");

builder.Configuration.AddInMemoryCollection(DashSpecTomlLoader.Flatten(dashSpecToml));

builder.Services.AddSingleton(accessOptions);
builder.Services.AddSingleton<DashSpecAccessValidator>();

builder.Services.AddSingleton(new DashSpecHostContext
{
    StartupRuntimeConfigPath = startupConfigPath,
    StartupRuntimeReference = startupRuntimeReference,
    DefaultSpecRelativePath = DashSpecBootstrap.ToHostSpecReference(
        builder.Environment.ContentRootPath,
        defaultSpecPath),
    DefaultSpecDirectory = Path.GetDirectoryName(defaultSpecPath)!,
    Catalog = catalog,
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

using var pluginLoggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
var pluginLogger = pluginLoggerFactory.CreateLogger("DashSpec.Plugins");

var contributorRegistry = DashSpecPluginLoader.RegisterPlugins(
    builder.Services,
    builder.Configuration,
    builder.Environment,
    DashSpecPluginLoader.LoadManifest(dashSpecToml),
    pluginLogger);

builder.Services.AddSingleton(new DashSpecParseOptionsProvider(contributorRegistry));

var connectorManifest = ConnectorPluginLoader.LoadManifest(dashSpecToml);
ConnectorPluginLoader.RegisterPlugins(
    builder.Services,
    builder.Configuration,
    builder.Environment,
    connectorManifest,
    NullLogger.Instance);

builder.Services.AddScoped<IDashboardSpecLoader, DashboardSpecLoader>();
builder.Services.AddScoped<ICardRenderer, CardRenderService>();
builder.Services.AddScoped<IDashboardSession, DashboardSessionService>();
builder.Services.AddScoped<DashboardFilterUiState>();
builder.Services.AddScoped<DashboardRefreshCoordinator>();
builder.Services.AddScoped<DashboardPageController>();
builder.Services.AddSingleton<LoadTrace>();
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
app.UseMiddleware<DashSpecAccessMiddleware>();
app.UseAntiforgery();
app.UseStaticFiles();

app.MapAccessEndpoints();
app.MapPluginEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDevEndpoints();

app.Run();
