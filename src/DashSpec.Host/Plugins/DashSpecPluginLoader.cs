using DashSpec.Abstractions.Plugins;
using DashSpec.Composition.Plugins;
using DashSpec.Viz;
using DashSpec.Host.Configuration;
using DashSpec.Host.Plugins.Builtins;
using DashSpec.Surface.Blazor.Composition;
using DashSpec.Surface.Blazor.Services.Presentation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DashSpec.Host.Plugins;

/// <summary>
/// Web-host plugin orchestration over the composition kernel (ADR-0117 step 2): the Host
/// keeps what it owns — the typed TOML root mapping, the diagnostics builtin and the
/// content-root-relative plugins directory — plus the surface UI binding the kernel must
/// not own. Vocabulary (builtins, bundles, external loads) lives in
/// <see cref="DashSpecPluginComposition"/> and is shared verbatim with dsc.
/// </summary>
public static class DashSpecPluginLoader
{
    public static DashSpecPluginManifest LoadManifest(DashSpecTomlRoot toml) =>
        new()
        {
            ActiveBundle = string.IsNullOrWhiteSpace(toml.Plugins.ActiveBundle)
                ? "standard"
                : toml.Plugins.ActiveBundle,
            DefaultProviderId = string.IsNullOrWhiteSpace(toml.Plugins.DefaultProviderId)
                ? "sqlserver"
                : toml.Plugins.DefaultProviderId,
            Bundles = toml.Plugins.Bundles
                .Select(x => new DashSpecBundleDefinition
                {
                    Name = x.Name,
                    Plugins = x.Plugins,
                })
                .ToList(),
            Plugins = toml.Plugins.Load
                .Select(x => new DashSpecPluginLoadEntry
                {
                    Id = x.Id,
                    Assembly = x.Assembly,
                    Tier = ParseTier(x.Tier),
                    IsProvider = x.IsProvider,
                })
                .ToList(),
        };

    public static DashSpecContributorRegistry RegisterPlugins(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        DashSpecPluginManifest manifest,
        ILogger? logger = null)
    {
        var kernel = DashSpecPluginComposition.Compose(
            services,
            configuration,
            manifest,
            ResolvePluginsDirectory(environment),
            logger,
            betweenBuiltinsAndExternals: (svc, registry) =>
            {
                // Surface UI binding (ADR-0117): razor catalogs and session services —
                // after kernel builtins, before external loads, matching the pre-split order.
                SurfaceCompositionServices.RegisterBuiltInPlugins(
                    registry,
                    svc,
                    configuration,
                    _ => { });
                svc.AddSingleton<OnClickInteractionService>();
                svc.AddSingleton<IOnClickInteractionService>(sp => sp.GetRequiredService<OnClickInteractionService>());
                svc.AddScoped<ICardViewState, CardViewStateService>();
                svc.AddScoped<ICardVizDisplayState, CardVizDisplayStateService>();
            });

        if (kernel.ActivePluginIds.Contains("dashspec_diagnostics") &&
            !kernel.Registry.ContainsPlugin("dashspec_diagnostics"))
        {
            RegisterBuiltIn(new DiagnosticsBuiltinPlugin(), kernel.Registry, services, configuration);
        }

        // Surface registries snapshot the kernel's component maps AFTER all plugin loads (external plugins included).
        SurfaceCompositionServices.RegisterComponentRegistries(services, kernel.Registry);

        return kernel.Registry;
    }

    private static void RegisterBuiltIn(
        IDashSpecPlugin plugin,
        DashSpecContributorRegistry registry,
        IServiceCollection services,
        IConfiguration configuration)
    {
        plugin.ConfigureServices(services, configuration);
        registry.RegisterPlugin(plugin);
    }

    private static string ResolvePluginsDirectory(IHostEnvironment environment)
    {
        var pluginsDir = Path.Combine(AppContext.BaseDirectory, "plugins");
        if (!Directory.Exists(pluginsDir))
        {
            pluginsDir = Path.Combine(environment.ContentRootPath, "plugins");
        }

        return pluginsDir;
    }

    private static PluginTier ParseTier(string? raw) =>
        raw?.ToLowerInvariant() switch
        {
            "core" => PluginTier.Core,
            "product" => PluginTier.Product,
            _ => PluginTier.Extended,
        };
}
