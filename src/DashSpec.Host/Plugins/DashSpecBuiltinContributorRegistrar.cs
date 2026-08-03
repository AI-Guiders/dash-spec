using DashSpec.Abstractions.Plugins;
using DashSpec.Host.Plugins.Builtins;

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
        registry.RegisterPlugin(new VizBuiltinPlugin());
        registry.RegisterPlugin(new FilterWidgetsBuiltinPlugin());
        registry.RegisterPlugin(new CardViewsBuiltinPlugin());
    }
}
