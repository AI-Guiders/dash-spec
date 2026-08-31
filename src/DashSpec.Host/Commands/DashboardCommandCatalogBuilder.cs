#nullable enable

using AIGuiders.Platform.CommandPlane;

using AIGuiders.Platform.CommandPlane.Sources;

using DashSpec.Abstractions.Plugins;

using DashSpec.Core.Model;



namespace DashSpec.Host.Commands;



internal static class DashboardCommandCatalogBuilder

{

    static readonly ICommandSource Bundled = LoadBundledSource();



    static readonly IReadOnlyList<SlashPickerChoice> DatePresetChoices = SlashPickerChoices.FromLabels(
        ("today", "Today"),
        ("last-week", "Last 7 days"),
        ("last-month", "Last month"));

    static readonly string[] FilterSurfaces = ["dash-slash", "dash-palette", "dash-ccl"];

    public static SlashCatalogIndex Build(
        DashboardFilterContext context,
        IReadOnlyList<DashSpecCommandDescriptor> pluginDescriptors)
    {
        var report = CommandSource.From(BuildReportDescriptors(context), "report");

        var plugins = CommandSource.From(

            pluginDescriptors.Select(ToSlashDescriptor).ToList(),

            "plugins");



        return SlashCatalogComposer.Build(Bundled, report, plugins);

    }



    static IReadOnlyList<SlashCommandDescriptor> BuildReportDescriptors(DashboardFilterContext context)

    {

        var descriptors = new List<SlashCommandDescriptor>();



        foreach (var entry in context.CatalogEntries)

        {

            descriptors.Add(NavDescriptor(
                SelectReportCommand.Id,
                $"select report {entry.Id}",
                entry.Title,
                "Report"));

        }



        foreach (var page in context.ReportPages)

        {

            descriptors.Add(NavDescriptor(
                SelectPageCommand.Id,
                $"select page {page.Id}",
                page.Title,
                "Report"));

        }



        foreach (var filterName in context.ToolbarFilterNames)

        {

            if (!context.FilterIndex.TryGetValue(filterName, out var filter))

            {

                continue;

            }



            if (filter.Kind is FilterKind.Date)

            {

                descriptors.Add(NavDescriptor(
                    SelectDateFilterCommand.Id,
                    FilterCommandPaths.FilterPath(filterName),
                    DashboardFilterSlashLabels.DateFilterHelp(context, filterName),
                    "Filter",
                    argTail: "picker+constructor:enum:date_preset+date_range",
                    argHint: DashboardFilterSlashLabels.DateFilterHint(context, filterName),
                    argPickerChoices: DatePresetChoices,
                    argConstructors:
                    [
                        new SlashConstructorBinding(
                            Constructors.DateConstructorCatalog.DateRangeId,
                            "Период…",
                            "Выбрать период с … по …"),
                    ]));

                continue;

            }



            if (filter.Kind is FilterKind.Field)

            {

                descriptors.Add(NavDescriptor(
                    $"dash.select.filter.{filterName}",
                    FilterCommandPaths.FilterPath(filterName),
                    DashboardFilterSlashLabels.FieldFilterHelp(context, filterName),
                    "Filter",
                    argTail: $"picker:dash.field.{filterName}",
                    argHint: DashboardFilterSlashLabels.FieldFilterHint(context, filterName)));

            }

        }



        foreach (var card in context.SwitchableCards)

        {

            foreach (var view in card.Views)

            {

                descriptors.Add(NavDescriptor(
                    SelectViewCommand.Id,
                    ViewCommandPaths.ViewPath(card.CardId, view.ViewId),
                    $"{card.Title} — {view.Label}",
                    "View"));

            }

        }



        return descriptors;

    }

    static SlashCommandDescriptor NavDescriptor(
        string commandId,
        string path,
        string help,
        string group,
        string? argTail = null,
        string? argHint = null,
        IReadOnlyList<SlashPickerChoice>? argPickerChoices = null,
        IReadOnlyList<SlashConstructorBinding>? argConstructors = null) =>
        new()
        {
            Domain = "",
            Object = "",
            Intent = "",
            CommandId = commandId,
            Path = path,
            Help = help,
            Group = group,
            ArgTail = argTail,
            ArgHint = argHint,
            ArgPickerChoices = argPickerChoices ?? [],
            ArgConstructors = argConstructors ?? [],
            Surfaces = FilterSurfaces,
        };

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

