using DashSpec.Abstractions.Plugins;
using DashSpec.Composition.Plugins;
using DashSpec.Viz;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DashSpec.Surface.Blazor.Composition;

/// <summary>UI-binding registries and the session dispatcher, built from the kernel's component maps (ADR-0117).</summary>
public static class SurfaceCompositionServices
{
    /// <summary>Auto-discovers and registers vertical UI plugins (`DashSpec.Plugin.*`) from loaded assemblies (ADR-0117 / ADR-0077).</summary>
    public static void RegisterBuiltInPlugins(
        DashSpecContributorRegistry registry,
        IServiceCollection services,
        IConfiguration configuration,
        Action<IDashSpecPlugin> onPluginRegistered)
    {
        void Register(IDashSpecPlugin plugin)
        {
            if (registry.ContainsPlugin(plugin.Id))
            {
                return;
            }

            plugin.ConfigureServices(services, configuration);
            registry.RegisterPlugin(plugin);
            onPluginRegistered(plugin);
        }

        // Ensure core builtin UI plugin assemblies referenced by the surface/kernel are loaded into AppDomain.
        _ = typeof(DashSpec.Plugin.Viz.Builtins.Plugins.VizBuiltinsPluginCatalog).Assembly;
        _ = typeof(DashSpec.Plugin.Filter.Builtins.FilterBuiltinsPluginCatalog).Assembly;
        _ = typeof(DashSpec.Plugin.Viz.EChartsUI.Plugins.EChartsVizDashSpecPlugin).Assembly;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = assembly.GetName().Name ?? string.Empty;
            if (!name.StartsWith("DashSpec.Plugin.", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var type in assembly.GetExportedTypes())
            {
                if (!type.IsAbstract &&
                    typeof(IDashSpecPlugin).IsAssignableFrom(type) &&
                    type.GetConstructor(Type.EmptyTypes) is not null &&
                    Activator.CreateInstance(type) is IDashSpecPlugin plugin)
                {
                    Register(plugin);
                }
            }
        }

        // The builtin chrome markup stands only when no stack bound its own controls —
        // viz plugins configure before this point, so TryAdd (a plain AddSingleton
        // would stomp the stack registration, DI last-wins).
        services.TryAddSingleton<DashSpec.Viz.ICardChromeControlsOverride>(
            new DashSpec.Viz.DefaultCardChromeControlsOverride());
    }

    public static void RegisterComponentRegistries(IServiceCollection services, DashSpecContributorRegistry registry)
    {
        var cardVizComponents = new CardVizComponentRegistry();
        foreach (var (rendererId, componentType) in registry.VizComponents)
        {
            cardVizComponents.Register(rendererId, componentType);
        }

        services.AddSingleton(cardVizComponents);
        services.AddSingleton<ICardVizComponentResolver>(cardVizComponents);

        var toolbars = new VizCardToolbarRegistry();
        foreach (var (rendererId, componentType) in registry.VizToolbars)
        {
            toolbars.Register(rendererId, componentType);
        }

        services.AddSingleton(toolbars);

        var filterWidgetComponents = new FilterWidgetComponentRegistry();
        foreach (var (widgetId, componentType) in registry.FilterWidgetComponents)
        {
            filterWidgetComponents.Register(widgetId, componentType);
        }

        services.AddSingleton(filterWidgetComponents);
        services.AddScoped<DashSpecActionDispatcher>();
    }
}
