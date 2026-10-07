using DashSpec.Abstractions.Plugins;
using DashSpec.Filters;
using DashSpec.Plugin.Filter.Builtins;
using DashSpec.Viz;
using DashSpec.Plugin.Viz.Builtins.Plugins;
using DashSpec.Viewer.Plugins.Builtins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DashSpec.Viewer.Plugins;

/// <summary>Core viewer plugin registration (ADR-0099 B3.2). Host adds planet-specific builtins and external loads.</summary>
public static class ViewerPluginBootstrap
{
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

        Register(new ScopeBuiltinPlugin());
        Register(new DiagramBuiltinPlugin());
        Register(new OnClickDefaultPlugin());
        foreach (var vizPlugin in VizBuiltinsPluginCatalog.CreateAll())
        {
            Register(vizPlugin);
        }

        foreach (var filterPlugin in FilterBuiltinsPluginCatalog.CreateAll())
        {
            Register(filterPlugin);
        }

        Register(new CardViewsBuiltinPlugin());
    }

    public static void RegisterViewerServices(IServiceCollection services, DashSpecContributorRegistry registry)
    {
        var cardVizComponents = registry.BuildCardVizComponentRegistry();
        services.AddSingleton(cardVizComponents);
        services.AddSingleton<ICardVizComponentResolver>(cardVizComponents);
        services.AddSingleton(registry.BuildVizCardToolbarRegistry());
        services.AddSingleton(registry.BuildFilterWidgetComponentRegistry());
        services.AddSingleton<FilterWidgetRegistry>();
        services.AddSingleton<IFilterWidgetComponentResolver>(sp => sp.GetRequiredService<FilterWidgetRegistry>());
        services.AddSingleton<VizPluginRegistry>();
        services.AddScoped<DashSpecActionDispatcher>();
    }
}
