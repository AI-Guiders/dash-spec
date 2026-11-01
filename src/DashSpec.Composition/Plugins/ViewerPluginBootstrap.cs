using DashSpec.Abstractions.Plugins;
using DashSpec.Composition.Plugins.Builtins;
using DashSpec.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DashSpec.Composition.Plugins;

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

        // Razor plugin catalogs (viz/filter/Radzen) register from DashSpec.Surface.Blazor (ADR-0117):
        // they are component libraries and would pull AspNetCore into the kernel.
        Register(new CardViewsBuiltinPlugin());
    }

    public static void RegisterViewerServices(IServiceCollection services, DashSpecContributorRegistry registry)
    {
        // UI-binding registries and the session dispatcher live in DashSpec.Surface.Blazor (ADR-0117);
        // the kernel registers only the logical registries.
        services.AddSingleton<FilterWidgetRegistry>();
        services.AddSingleton<IFilterWidgetComponentResolver>(sp => sp.GetRequiredService<FilterWidgetRegistry>());
        services.AddSingleton<VizPluginRegistry>();
    }
}
