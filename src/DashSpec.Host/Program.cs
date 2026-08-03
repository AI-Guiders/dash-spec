using DashSpec.Core.Parsing;
using DashSpec.Core.Validation;
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

if (args is ["validate", var validatePath, ..])
{
    try
    {
        var fullPath = Path.GetFullPath(validatePath);
        if (fullPath.EndsWith(".dashcatalog", StringComparison.OrdinalIgnoreCase))
        {
            DashSpecValidator.ValidateCatalog(fullPath);
        }
        else
        {
            var registry = DashSpecBuiltinContributorRegistrar.RegisterBuiltins();
            var parseOptions = new DashSpecParseOptionsProvider(registry).CreateOptions();
            var specDirectory = Path.GetDirectoryName(fullPath)!;
            DashSpecValidator.ValidateSpec(fullPath, specDirectory, parseOptions);
        }

        Console.WriteLine($"OK: {fullPath}");
        return;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        Environment.ExitCode = 1;
        return;
    }
}

var builder = WebApplication.CreateBuilder(args);

if (OperatingSystem.IsWindows())
{
    builder.Host.UseWindowsService(options => options.ServiceName = "UrsaLicenseUsageDashSpec");
}

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

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IFieldOptionsCache, FieldOptionsCache>();
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
}

// HTTP-only prod (ASPNETCORE_URLS=http://*:5295): no HSTS / HTTPS redirect — otherwise login cookie Secure=true is dropped by browsers.
var urlsEnv = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? string.Empty;
if (urlsEnv.Contains("https://", StringComparison.OrdinalIgnoreCase))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseMiddleware<DashSpecAccessMiddleware>();
app.UseAntiforgery();
app.UseStaticFiles();

app.MapAccessEndpoints();
app.MapPluginEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDevEndpoints();

app.Run();
