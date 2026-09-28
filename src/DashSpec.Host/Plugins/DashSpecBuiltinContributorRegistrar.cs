using DashSpec.Abstractions.Plugins;
using DashSpec.Host.Plugins.Builtins;
using DashSpec.Plugin.Viz.Builtins.Plugins;

namespace DashSpec.Host.Plugins;

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

        registry.RegisterPlugin(new FilterWidgetsBuiltinPlugin());
        registry.RegisterPlugin(new CardViewsBuiltinPlugin());
    }
}
