#nullable enable
using AIGuiders.Platform.CommandPlane.Commands;

namespace DashSpec.Host.Commands;

internal static class DashboardCommandRegistryFactory
{
    public static PlatformCommandRegistry<DashboardFilterContext> Create(
        IEnumerable<string> fieldSlashAliases,
        IEnumerable<IPlatformCommand<DashboardFilterContext>> pluginCommands)
    {
        var registry = new PlatformCommandRegistry<DashboardFilterContext>();
        registry.Register(new SelectDateFilterCommand());

        foreach (var alias in fieldSlashAliases
                     .Where(alias => !string.IsNullOrWhiteSpace(alias))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            registry.Register(new SelectFieldFilterCommand(alias.Trim()));
        }

        foreach (var command in pluginCommands)
        {
            registry.Register(command);
        }

        return registry;
    }
}
