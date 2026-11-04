using DashSpec.Abstractions.Plugins;
using DashSpec.Composition.Plugins;
using DashSpec.Plugin.Filter.Builtins;
using DashSpec.Plugin.Viz.Builtins.Plugins;
using DashSpec.Viz;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DashSpec.Surface.Blazor.Composition;

/// <summary>UI-binding registries and the session dispatcher, built from the kernel's component maps (ADR-0117).</summary>
public static class SurfaceCompositionServices
{
    /// <summary>Builtin Razor plugin catalogs — UI libraries, registered by the surface (ADR-0117).</summary>
    public static void RegisterBuiltInPlugins(
        DashSpecContributorRegistry registry,
        IServiceCollection services,
        IConfiguration configuration,
        Action<IDashSpecPlugin> onPluginRegistered)
    {
        void Register(IDashSpecPlugin plugin)
        {
            plugin.ConfigureServices(services, configuration);
            registry.RegisterPlugin(plugin);
            onPluginRegistered(plugin);
        }

        // Card diagram chrome is surface-owned (ADR-0117, vertical plugins): one renderer-agnostic
        // host for every renderer — the builtin CardVizHost. Plugins never register card hosts.
        registry.RegisterPluginComponent(
            DashSpecPluginComponents.CardDiagram,
            typeof(DashSpec.Plugin.Viz.Builtins.Components.Hosting.CardVizHost));

        foreach (var vizPlugin in VizBuiltinsPluginCatalog.CreateAll())
        {
            Register(vizPlugin);
        }

        // Radzen visual layer pilot ([ADR-0111]): bundle id (viz_radzen) activates it, render = "radzen".
        Register(new DashSpec.Plugin.Viz.RadzenUI.Plugins.RadzenVizDashSpecPlugin());

        // ECharts matrix guest (ADR-0112 spike): bundle id (viz_echarts); stacks delegate the heatmap kind here.
        Register(new DashSpec.Plugin.Viz.EChartsUI.Plugins.EChartsVizDashSpecPlugin());

        foreach (var filterPlugin in FilterBuiltinsPluginCatalog.CreateAll())
        {
            Register(filterPlugin);
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
