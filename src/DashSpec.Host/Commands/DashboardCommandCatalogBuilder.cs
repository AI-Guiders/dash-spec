#nullable enable

using AIGuiders.Platform.CommandPlane;
using AIGuiders.Platform.CommandPlane.Commands;
using DashSpec.Abstractions.Plugins;

namespace DashSpec.Host.Commands;

internal static class DashboardCommandCatalogBuilder
{
    public static CommandCatalogIndex Build(
        DashboardFilterContext context,
        IReadOnlyList<DashSpecCommandDescriptor> pluginDescriptors,
        IEnumerable<IPlatformCommand<DashboardFilterContext>>? pluginCommands = null)
    {
        var registry = DashboardCommandRegistryFactory.Create(context, pluginCommands ?? []);
        var pluginList = pluginDescriptors.Select(ToCommandDescriptor).ToList();
        if (context.ActiveScope.Count > 0)
        {
            pluginList = CommandScopeFilter.WhereScope(pluginList, context.ActiveScope).ToList();
        }

        var plugins = CommandSource.From(pluginList, "plugins");

        return CommandCatalogAssembly.Build(
            registry,
            DashboardCommandCatalogExpander.Expand(context),
            context.ActiveScope,
            plugins);
    }

    static CommandDescriptor ToCommandDescriptor(DashSpecCommandDescriptor descriptor) =>
        CommandDescriptors.Describe(descriptor.CommandId)
            .Domain("dash")
            .Object("plugin")
            .Intent(descriptor.CommandId)
            .Path(descriptor.Path)
            .PathAliases(descriptor.PathAliases ?? [])
            .Help(descriptor.Help)
            .ArgTail(descriptor.ArgTail)
            .Group(descriptor.Group ?? "Plugins")
            .PluginId(descriptor.PluginId)
            .Surfaces(DashboardCommandCatalogExpander.DashSurfaces)
            .Scope(DashSpecCommandScope.Dashboard)
            .Build();
}
