#nullable enable
using AIGuiders.Platform.CommandPlane;
using AIGuiders.Platform.CommandPlane.Commands;
using DashSpec.Abstractions.Plugins;

namespace DashSpec.Host.Commands;

internal static class DashboardCommandCatalogBuilder
{
    public static SlashCatalogIndex Build(
        DashboardFilterContext context,
        IReadOnlyList<DashSpecCommandDescriptor> pluginDescriptors)
    {
        var descriptors = new List<SlashCommandDescriptor>
        {
            new()
            {
                Domain = "dash",
                Object = "select",
                Intent = "date",
                CommandId = SelectDateFilterCommand.Id,
                Path = "select date",
                Help = "Set date filter (today, last-week, YYYY-MM, range)",
                ArgTail = "picker:enum:date_preset",
                ArgHint = "Preset, YYYY-MM, or from..to range",
                ArgPickerChoices = SlashPickerChoices.FromLabels(
                    ("today", "Today"),
                    ("last-week", "Last 7 days"),
                    ("last-month", "Last month")),
                Group = "Filters",
            },
        };

        foreach (var alias in DashboardCommandAliasResolver.ResolveFieldSlashAliases(context))
        {
            descriptors.Add(new SlashCommandDescriptor
            {
                Domain = "dash",
                Object = "select",
                Intent = alias,
                CommandId = $"dash.select.{alias}",
                Path = $"select {alias}",
                Help = $"Set {alias} filter",
                ArgTail = $"picker:dash.field.{alias}",
                ArgHint = $"Choose a {alias} value or type to filter",
                Group = "Filters",
            });
        }

        foreach (var plugin in pluginDescriptors)
        {
            descriptors.Add(ToSlashDescriptor(plugin));
        }

        return SlashCatalogIndex.FromDescriptors(descriptors);
    }

    static SlashCommandDescriptor ToSlashDescriptor(DashSpecCommandDescriptor descriptor) =>
        new()
        {
            Domain = "dash",
            Object = "plugin",
            Intent = descriptor.CommandId,
            CommandId = descriptor.CommandId,
            Path = descriptor.Path,
            PathAliases = descriptor.PathAliases ?? [],
            Help = descriptor.Help,
            ArgTail = descriptor.ArgTail,
            Group = descriptor.Group ?? "Plugins",
            PluginId = descriptor.PluginId,
        };
}
