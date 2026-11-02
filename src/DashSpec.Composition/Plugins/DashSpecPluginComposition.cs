using System.Reflection;
using System.Runtime.Loader;
using DashSpec.Abstractions.Plugins;
using DashSpec.Abstractions.Viewer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashSpec.Composition.Plugins;

/// <summary>
/// Result of composing the plugin kernel: the contributor registry roots consume, plus the
/// data the roots still need for their own steps (diagnostics builtins, surface snapshot).
/// </summary>
public sealed record DashSpecPluginKernel(
    DashSpecContributorRegistry Registry,
    DashSpecPluginManifest Manifest,
    DashSpecPluginCapabilities Capabilities,
    IReadOnlySet<string> ActivePluginIds);

/// <summary>
/// Plugin composition kernel (ADR-0117 step 2): bundle/tier resolution, builtins and external
/// assembly loads — the policy every composition root (web Host, dsc CLI) runs identically,
/// so CLI validation and the runtime see the same vocabulary. No UI-stack types here: surface
/// services and razor registries belong to the surface/roots and ride
/// <paramref name="betweenBuiltinsAndExternals"/> to keep the original registration order.
/// </summary>
public static class DashSpecPluginComposition
{
    public static DashSpecPluginKernel Compose(
        IServiceCollection services,
        IConfiguration configuration,
        DashSpecPluginManifest manifest,
        string? pluginsDirectory,
        ILogger? logger = null,
        Action<IServiceCollection, DashSpecContributorRegistry>? betweenBuiltinsAndExternals = null,
        Action<IDashSpecPlugin>? onPluginRegistered = null)
    {
        logger ??= NullLogger.Instance;
        var registry = new DashSpecContributorRegistry();

        ViewerPluginBootstrap.RegisterBuiltInPlugins(registry, services, configuration, onPluginRegistered ?? (_ => { }));

        betweenBuiltinsAndExternals?.Invoke(services, registry);

        ViewerPluginBootstrap.RegisterViewerServices(services, registry);

        var activeBundle = ResolveActiveBundle(manifest);
        var pluginIds = ResolveBundlePluginIds(manifest, activeBundle);

        if (pluginsDirectory is not null)
        {
            EnsureSharedAssemblyResolution(logger);

            foreach (var entry in manifest.Plugins.Where(x => pluginIds.Contains(x.Id, StringComparer.OrdinalIgnoreCase)))
            {
                if (entry.IsProvider)
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

                LoadExternalPlugin(services, configuration, registry, pluginsDirectory, entry, logger, onPluginRegistered);
            }
        }

        services.AddSingleton(registry);
        services.AddSingleton(manifest);
        var capabilities = registry.BuildCapabilities(activeBundle);
        services.AddSingleton(capabilities);
        services.AddSingleton<IViewerPluginHost>(sp => new ViewerPluginHost(
            sp.GetRequiredService<DashSpecContributorRegistry>(),
            sp.GetRequiredService<DashSpecPluginCapabilities>()));

        return new DashSpecPluginKernel(registry, manifest, capabilities, pluginIds);
    }

    private static void LoadExternalPlugin(
        IServiceCollection services,
        IConfiguration configuration,
        DashSpecContributorRegistry registry,
        string pluginsDir,
        DashSpecPluginLoadEntry entry,
        ILogger logger,
        Action<IDashSpecPlugin>? onPluginRegistered)
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
            onPluginRegistered?.Invoke(plugin);
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

    internal static string ResolveActiveBundle(DashSpecPluginManifest manifest)
    {
        return string.IsNullOrWhiteSpace(manifest.ActiveBundle) ? "standard" : manifest.ActiveBundle;
    }

    internal static HashSet<string> ResolveBundlePluginIds(DashSpecPluginManifest manifest, string activeBundle)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "scope_builtin",
            "diagram_builtin",
            "on_click_default",
            "viz_chartjs",
            "viz_matrix_canvas",
            "viz_css_grid",
            "viz_table_html",
            "viz_scalar_html",
            "viz_gantt_html",
            "viz_gantt_timeline",
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
}
