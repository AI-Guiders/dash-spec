#nullable enable
using AIGuiders.Platform.CommandPlane;
using AIGuiders.Platform.CommandPlane.Sources;
using DashSpec.Abstractions.Plugins;

namespace DashSpec.Host.Commands;

internal static class DashboardCommandCatalogBuilder
{
    static readonly ICommandSource Bundled = LoadBundledSource();

    static readonly string[] FilterSurfaces = ["dash-slash", "dash-palette", "dash-ccl"];

    public static SlashCatalogIndex Build(
        DashboardFilterContext context,
        IReadOnlyList<DashSpecCommandDescriptor> pluginDescriptors)
    {
        var report = CommandSource.From(BuildReportFieldDescriptors(context), "report");
        var plugins = CommandSource.From(
            pluginDescriptors.Select(ToSlashDescriptor).ToList(),
            "plugins");

        return SlashCatalogComposer.Build(Bundled, report, plugins);
    }

    static IReadOnlyList<SlashCommandDescriptor> BuildReportFieldDescriptors(DashboardFilterContext context)
    {
        var descriptors = new List<SlashCommandDescriptor>();

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
                Surfaces = FilterSurfaces,
            });
        }

        return descriptors;
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
            Surfaces = FilterSurfaces,
        };

    static ICommandSource LoadBundledSource()
    {
        const string resourceSuffix = "dash-filter-commands.toml";
        var assembly = typeof(DashboardCommandCatalogBuilder).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Embedded catalog '{resourceSuffix}' was not found.");

        using var stream = assembly.GetManifestResourceStream(resourceName)
                         ?? throw new InvalidOperationException($"Embedded catalog '{resourceName}' could not be opened.");
        using var reader = new StreamReader(stream);
        return CommandSources.FromToml(reader.ReadToEnd(), "bundled");
    }
}
