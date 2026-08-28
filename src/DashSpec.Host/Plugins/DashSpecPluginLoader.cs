using System.Reflection;
using System.Runtime.Loader;
using DashSpec.Abstractions.Connectors;
using DashSpec.Abstractions.Plugins;
using DashSpec.Host.Configuration;
using DashSpec.Host.Commands;
using DashSpec.Host.Plugins.Builtins;
using DashSpec.Host.Services.Presentation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashSpec.Host.Plugins;

public static class DashSpecPluginLoader
{
    public static DashSpecPluginManifest LoadManifest(DashSpecTomlRoot toml) =>
        new()
        {
            ActiveBundle = string.IsNullOrWhiteSpace(toml.Plugins.ActiveBundle)
                ? "standard"
                : toml.Plugins.ActiveBundle,
            DefaultConnectorId = string.IsNullOrWhiteSpace(toml.Plugins.DefaultConnectorId)
                ? "sqlserver"
                : toml.Plugins.DefaultConnectorId,
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
                    IsConnector = x.IsConnector,
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
        logger ??= NullLogger.Instance;
        var registry = new DashSpecContributorRegistry();
        var commandRegistry = new DashSpecCommandPluginRegistry();

        RegisterBuiltIn(new ScopeBuiltinPlugin(), registry, services, configuration, commandRegistry);
        RegisterBuiltIn(new DiagramBuiltinPlugin(), registry, services, configuration, commandRegistry);
        RegisterBuiltIn(new OnClickDefaultPlugin(), registry, services, configuration, commandRegistry);
        RegisterBuiltIn(new VizBuiltinPlugin(), registry, services, configuration, commandRegistry);
        RegisterBuiltIn(new FilterWidgetsBuiltinPlugin(), registry, services, configuration, commandRegistry);
        RegisterBuiltIn(new CardViewsBuiltinPlugin(), registry, services, configuration, commandRegistry);

        services.AddScoped<ICardViewState, CardViewStateService>();

        var activeBundle = ResolveActiveBundle(manifest);
        var pluginIds = ResolveBundlePluginIds(manifest, activeBundle);
        var pluginsDir = ResolvePluginsDirectory(environment);

        EnsureSharedAssemblyResolution(logger);

        foreach (var entry in manifest.Plugins.Where(x => pluginIds.Contains(x.Id, StringComparer.OrdinalIgnoreCase)))
        {
            if (entry.IsConnector)
            {
                continue;
            }

            if (registry.ContainsPlugin(entry.Id))
            {
                logger.LogDebug(
                    "Skipping external load for plugin '{PluginId}' — already registered as built-in.",
                    entry.Id);
                continue;
            }

            LoadExternalPlugin(services, configuration, registry, commandRegistry, pluginsDir, entry, logger);
        }

        if (pluginIds.Contains("dashspec_diagnostics", StringComparer.OrdinalIgnoreCase) &&
            !registry.ContainsPlugin("dashspec_diagnostics"))
        {
            RegisterBuiltIn(new DiagnosticsBuiltinPlugin(), registry, services, configuration, commandRegistry);
        }

        services.AddSingleton(commandRegistry);
        services.AddSingleton(registry);
        services.AddSingleton(manifest);
        services.AddSingleton(sp => sp.GetRequiredService<DashSpecContributorRegistry>().BuildCapabilities(activeBundle));
        services.AddSingleton<VizPluginRegistry>();
        services.AddScoped<DashSpecActionDispatcher>();

        return registry;
    }

    private static void RegisterBuiltIn(
        IDashSpecPlugin plugin,
        DashSpecContributorRegistry registry,
        IServiceCollection services,
        IConfiguration configuration,
        DashSpecCommandPluginRegistry commandRegistry)
    {
        plugin.ConfigureServices(services, configuration);
        registry.RegisterPlugin(plugin);
        RegisterCommandPlugin(plugin, commandRegistry);
    }

    private static void RegisterCommandPlugin(
        IDashSpecPlugin plugin,
        DashSpecCommandPluginRegistry commandRegistry)
    {
        if (plugin is IDashSpecCommandPlugin commandPlugin)
        {
            commandPlugin.RegisterCommands(commandRegistry);
        }
    }

    private static void LoadExternalPlugin(
        IServiceCollection services,
        IConfiguration configuration,
        DashSpecContributorRegistry registry,
        DashSpecCommandPluginRegistry commandRegistry,
        string pluginsDir,
        DashSpecPluginLoadEntry entry,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(entry.Assembly))
        {
            logger.LogWarning("DashSpec plugin '{PluginId}' has no assembly path.", entry.Id);
            return;
        }

        var assemblyPath = Path.IsPathRooted(entry.Assembly)
            ? entry.Assembly
            : Path.Combine(pluginsDir, entry.Assembly);

        if (!File.Exists(assemblyPath))
        {
            logger.LogWarning("DashSpec plugin assembly not found: {AssemblyPath}", assemblyPath);
            return;
        }

        Assembly assembly;
        try
        {
            assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load DashSpec plugin assembly: {AssemblyPath}", assemblyPath);
            return;
        }

        var pluginTypes = GetLoadablePluginTypes(assembly, assemblyPath, logger);
        var loaded = false;

        foreach (var pluginType in pluginTypes)
        {
            if (Activator.CreateInstance(pluginType) is not IDashSpecPlugin plugin)
            {
                logger.LogWarning(
                    "Plugin type {PluginType} in {AssemblyPath} does not implement {Interface}.",
                    pluginType.FullName,
                    assemblyPath,
                    nameof(IDashSpecPlugin));
                continue;
            }

            if (!string.Equals(plugin.Id, entry.Id, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning(
                    "Skipping plugin type {PluginType} (id {PluginId}, expected {ExpectedId})",
                    pluginType.FullName,
                    plugin.Id,
                    entry.Id);
                continue;
            }

            plugin.ConfigureServices(services, configuration);
            registry.RegisterPlugin(plugin);
            RegisterCommandPlugin(plugin, commandRegistry);
            loaded = true;
            logger.LogInformation("Loaded DashSpec plugin {PluginId} from {Assembly}", plugin.Id, assemblyPath);
        }

        if (!loaded)
        {
            logger.LogWarning(
                "No DashSpec plugin with id '{PluginId}' found in {AssemblyPath}.",
                entry.Id,
                assemblyPath);
        }
    }

    private static bool _sharedAssemblyResolutionRegistered;

    private static void EnsureSharedAssemblyResolution(ILogger logger)
    {
        if (_sharedAssemblyResolutionRegistered)
        {
            return;
        }

        _sharedAssemblyResolutionRegistered = true;
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            var sharedPath = Path.Combine(AppContext.BaseDirectory, $"{name.Name}.dll");
            if (!File.Exists(sharedPath))
            {
                return null;
            }

            logger.LogDebug("Resolving plugin dependency {AssemblyName} from {AssemblyPath}", name.Name, sharedPath);
            return AssemblyLoadContext.Default.LoadFromAssemblyPath(sharedPath);
        };
    }

    private static IEnumerable<Type> GetLoadablePluginTypes(Assembly assembly, string assemblyPath, ILogger logger)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            logger.LogError(
                ex,
                "Partial type load failure in {AssemblyPath}: {Errors}",
                assemblyPath,
                string.Join("; ", ex.LoaderExceptions?.Select(error => error?.Message) ?? []));
            types = ex.Types.Where(type => type is not null).Cast<Type>().ToArray();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enumerate types in {AssemblyPath}", assemblyPath);
            return [];
        }

        return types.Where(type => type is { IsAbstract: false, IsInterface: false } &&
                                   typeof(IDashSpecPlugin).IsAssignableFrom(type));
    }

    private static string ResolveActiveBundle(DashSpecPluginManifest manifest)
    {
        var envBundle = Environment.GetEnvironmentVariable("DASHSPEC_PLUGIN_BUNDLE");
        if (!string.IsNullOrWhiteSpace(envBundle))
        {
            return envBundle;
        }

        return string.IsNullOrWhiteSpace(manifest.ActiveBundle) ? "standard" : manifest.ActiveBundle;
    }

    private static HashSet<string> ResolveBundlePluginIds(DashSpecPluginManifest manifest, string activeBundle)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "scope_builtin",
            "diagram_builtin",
            "on_click_default",
            "viz_builtin",
            "filter_widgets_builtin",
        };

        var bundle = manifest.Bundles.FirstOrDefault(x =>
            string.Equals(x.Name, activeBundle, StringComparison.OrdinalIgnoreCase));
        if (bundle is null)
        {
            return ids;
        }

        foreach (var pluginId in bundle.Plugins)
        {
            ids.Add(pluginId);
        }

        return ids;
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
