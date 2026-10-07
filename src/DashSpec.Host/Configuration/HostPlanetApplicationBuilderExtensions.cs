using System.Globalization;
using DashSpec.Abstractions.Hosting;
using DashSpec.Abstractions.Viewer;
using DashSpec.Core.Catalog;
using DashSpec.Core.Localization;
using DashSpec.Core.Runtime;
using DashSpec.Execution.Runtime;
using DashSpec.Host.Data;
using DashSpec.Host.Plugins;
using DashSpec.Host.Security;
using DashSpec.Host.Services.Abstractions;
using DashSpec.Host.Services.Connectors;
using DashSpec.Host.Services.Dev;
using DashSpec.Host.Services.Diagnostics;
using DashSpec.Host.Services.Git;
using DashSpec.Host.Services.Health;
using DashSpec.Host.Services.Loading;
using DashSpec.Viewer.Plugins;
using DashSpec.Host.Services.Settings;
using DashSpec.Surface.Blazor;
using DashSpec.Surface.Blazor.Commands;
using DashSpec.Surface.Blazor.Configuration;
using DashSpec.Surface.Blazor.Services.Presentation;
using DashSpecParser = DashSpec.Execution.Parsing.DashSpecParser;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OutWit.Database.EntityFramework.Extensions;

namespace DashSpec.Host.Configuration;

/// <summary>Planet deploy entry: bootstrap, DB, git, connectors, viewer wiring (ADR-0099 B3.4).</summary>
public static class HostPlanetApplicationBuilderExtensions
{
    public static async Task<DashSpecPlanetStartup> AddDashSpecPlanetHostAsync(
        this WebApplicationBuilder builder)
    {
        if (OperatingSystem.IsWindows())
        {
            builder.Host.UseWindowsService(options => options.ServiceName = "UrsaLicenseUsageDashSpec");
        }

        var bootstrapServices = new ServiceCollection();
        bootstrapServices.AddLogging();
        bootstrapServices.AddDashSpecHostInfrastructure();
#pragma warning disable ASP0000
        await using var bootstrapProvider = bootstrapServices.BuildServiceProvider();
#pragma warning restore ASP0000

        var hostBootstrap = bootstrapProvider.GetRequiredService<IHostBootstrap>();
        var hostDatabase = bootstrapProvider.GetRequiredService<IHostDatabaseInitializer>();
        var tomlLoader = bootstrapProvider.GetRequiredService<IDashSpecTomlLoader>();
        var pathResolver = bootstrapProvider.GetRequiredService<IHostPathResolver>();

        var (bootstrap, hostShell) = hostBootstrap.LoadBootstrapWithHost(builder.Environment);

        var catalog = hostBootstrap.LoadCatalog(bootstrap, builder.Environment.ContentRootPath);
        var catalogState = new CatalogSourceState(catalog);
        var defaultSpecPath = hostBootstrap.ResolveActiveSpecFullPath(catalog);
        var dashSpecToml = hostBootstrap.Load(builder.Environment);
        var defaultSpecText = await File.ReadAllTextAsync(defaultSpecPath);
        var startupConfigPath = pathResolver.ResolveRuntimeConfigPath(defaultSpecPath, defaultSpecText);
        var startupRuntimeReference = DashSpecParser.ReadRuntimePath(defaultSpecText)
            ?? throw new InvalidOperationException("Default catalog entry .dashspec must declare @runtime.");

        var accessOptions = new DashSpecAccessOptions { ApiKey = bootstrap.Access.ApiKey };
        builder.Configuration.AddInMemoryCollection(tomlLoader.Flatten(dashSpecToml));

        var uiCulture = ResolveUiCulture(bootstrap.Presentation.Language);
        var displayTimeZone = DashboardCultureAmbient.ResolveTimeZone(bootstrap.Presentation.DisplayTimeZone);
        LabelFormat.DisplayTimeZone = displayTimeZone;
        LabelFormat.UiCulture = uiCulture;
        TooltipTemplate.CellValueFormatter = static value => LabelFormat.FormatObject(value);

        builder.Services.AddLocalization();
        builder.Services.Configure<RequestLocalizationOptions>(options =>
        {
            var supported = new[]
            {
                CultureInfo.GetCultureInfo("ru-RU"),
                CultureInfo.GetCultureInfo("en-US"),
                CultureInfo.GetCultureInfo("en-GB"),
            };
            options.SupportedCultures = supported;
            options.SupportedUICultures = supported;
            options.DefaultRequestCulture = new RequestCulture(uiCulture);
        });

        var defaultSpecRelativePath = pathResolver.ToHostSpecReference(
            builder.Environment.ContentRootPath,
            defaultSpecPath);
        var defaultSpecDirectory = Path.GetDirectoryName(defaultSpecPath)!;

        builder.Services.AddSingleton(bootstrap);
        builder.Services.AddSingleton(hostShell);
        builder.Services.AddSingleton(catalogState);
        builder.Services.AddSingleton(accessOptions);
        builder.Services.AddSingleton<DashSpecAccessValidator>();
        builder.Services.AddSingleton(new DashSpecHostContext
        {
            StartupRuntimeConfigPath = startupConfigPath,
            StartupRuntimeReference = startupRuntimeReference,
            DefaultSpecRelativePath = defaultSpecRelativePath,
            DefaultSpecDirectory = defaultSpecDirectory,
            Catalog = catalog,
            HostShell = hostShell,
        });

        builder.Services.AddDashSpecBlazorViewerShell();
        builder.Services.AddDashSpecHostViewerPorts();

        var dataProtectionKeys = Path.Combine(builder.Environment.ContentRootPath, "data-protection-keys");
        Directory.CreateDirectory(dataProtectionKeys);
        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeys))
            .SetApplicationName("DashSpec.Host");

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
        builder.Services.AddDashSpecHostViewerPlatform();
        builder.Services.AddDashSpecHostViewerSession(uiCulture, displayTimeZone);
        builder.Services.AddSingleton<DashSpecCatalogHealthService>();
        builder.Services.AddSingleton<DevSpecReloadNotifier>();
        builder.Services.AddHttpClient();

        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddSingleton<DevSpecResolveService>();
            builder.Services.AddHostedService<DevSpecFileWatcherService>();
        }

        builder.Services.AddSingleton<IDevSpecReloadSignal>(sp =>
            builder.Environment.IsDevelopment()
                ? new DevSpecReloadSignalAdapter(sp.GetRequiredService<DevSpecReloadNotifier>())
                : new NullDevSpecReloadSignal());

        builder.Services.AddSingleton<GitCatalogSyncService>();
        builder.Services.AddHostedService<GitCatalogSyncBackgroundService>();
        builder.Services.AddSingleton<HostExternalLinksProvider>();

        builder.Services.AddSingleton(hostDatabase);
        builder.Services.AddDashSpecHostInfrastructure();
        var hostDbPath = hostDatabase.ResolveDatabasePath(bootstrap);
        hostDatabase.EnsureDatabase(hostDbPath);
        builder.Services.AddDbContext<DashSpecHostDbContext>(options =>
            options.UseWitDb($"Data Source={hostDbPath}"));
        builder.Services.AddScoped<HostSettingsService>();
        builder.Services.AddScoped<CatalogUsageService>();
        builder.Services.AddHttpContextAccessor();

        return new DashSpecPlanetStartup
        {
            Bootstrap = bootstrap,
            HostShell = hostShell,
            Catalog = catalog,
            CatalogState = catalogState,
            DefaultSpecPath = defaultSpecPath,
            StartupConfigPath = startupConfigPath,
            StartupRuntimeReference = startupRuntimeReference,
            DefaultSpecRelativePath = defaultSpecRelativePath,
            DefaultSpecDirectory = defaultSpecDirectory,
            UiCulture = uiCulture,
            DisplayTimeZone = displayTimeZone,
            AccessOptions = accessOptions,
            HostDatabasePath = hostDbPath,
            HostDatabase = hostDatabase,
        };
    }

    private static CultureInfo ResolveUiCulture(string? language) =>
        DashSpecCultures.Parse(
            string.IsNullOrWhiteSpace(language) ? DashSpecCultures.BootstrapDefaultName : language);
}
