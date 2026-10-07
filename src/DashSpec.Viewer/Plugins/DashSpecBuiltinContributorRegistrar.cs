using DashSpec.Abstractions.Plugins;
using DashSpec.Viewer.Plugins.Builtins;
using DashSpec.Plugin.Filter.Builtins;
using DashSpec.Plugin.Viz.Builtins.Plugins;

namespace DashSpec.Viewer.Plugins;

/// <summary>Registers built-in DashSpec plugins without external assemblies (CLI validate, Host startup).</summary>
public static class DashSpecBuiltinContributorRegistrar
{
    public static DashSpecContributorRegistry RegisterBuiltins()
    {
        var registry = new DashSpecContributorRegistry();
        RegisterBuiltins(registry);
        return registry;
    }

    public static void RegisterBuiltins(DashSpecContributorRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.RegisterPlugin(new ScopeBuiltinPlugin());
        registry.RegisterPlugin(new DiagramBuiltinPlugin());
        registry.RegisterPlugin(new OnClickDefaultPlugin());
        foreach (var vizPlugin in VizBuiltinsPluginCatalog.CreateAll())
        {
            registry.RegisterPlugin(vizPlugin);
        }

        foreach (var filterPlugin in FilterBuiltinsPluginCatalog.CreateAll())
        {
            registry.RegisterPlugin(filterPlugin);
        }

        registry.RegisterPlugin(new CardViewsBuiltinPlugin());
    }
}
