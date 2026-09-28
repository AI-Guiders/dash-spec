using DashSpec.Abstractions.Plugins;
using DashSpec.Plugin.Filter.Builtins.Plugins;

namespace DashSpec.Plugin.Filter.Builtins;

public static class FilterBuiltinsPluginCatalog
{
    public static IReadOnlyList<IDashSpecPlugin> CreateAll() =>
    [
        new FilterWidgetsBuiltinPlugin(),
    ];
}
