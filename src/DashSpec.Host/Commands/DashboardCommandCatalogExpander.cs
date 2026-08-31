#nullable enable

using AIGuiders.Platform.CommandPlane;
using AIGuiders.Platform.IntermediateRepresentation.Command;
using DashSpec.Core.Model;
using DashSpec.Host.Commands.Constructors;

namespace DashSpec.Host.Commands;

/// <summary>Context-bound catalog rows — federation builder, product data (GUIDERS-ADR-0045).</summary>
internal static class DashboardCommandCatalogExpander
{
    internal static readonly string[] DashSurfaces = ["dash-slash", "dash-palette", "dash-ccl"];

    public static IReadOnlyList<CommandDescriptor> Expand(DashboardFilterContext context)
    {
        var descriptors = new List<CommandDescriptor>();

        descriptors.AddRange(CommandDescriptorRows.Map(
            ShowHostSurfaceCommand.Id,
            HostSurfaceCatalog.Surfaces,
            surface => ShowCommandPaths.SurfacePath(surface.Id),
            surface => surface.Hint,
            (builder, _) => builder.Group("Host").Surfaces(DashSurfaces).Scope([])));

        descriptors.AddRange(CommandDescriptorRows.Map(
            SelectReportCommand.Id,
            context.CatalogEntries,
            entry => $"select report {entry.Id}",
            entry => entry.Title,
            (builder, _) => DashboardDefaults(builder)));

        descriptors.AddRange(CommandDescriptorRows.Map(
            SelectPageCommand.Id,
            context.ReportPages,
            page => $"select page {page.Id}",
            page => page.Title,
            (builder, _) => DashboardDefaults(builder)));

        foreach (var filterName in context.ToolbarFilterNames)
        {
            if (!context.FilterIndex.TryGetValue(filterName, out var filter))
            {
                continue;
            }

            if (filter.Kind is FilterKind.Date)
            {
                descriptors.Add(DateFilter(context, filterName));
                continue;
            }
        }

        foreach (var card in context.SwitchableCards)
        {
            descriptors.AddRange(CommandDescriptorRows.Map(
                SelectViewCommand.Id,
                card.Views,
                view => ViewCommandPaths.ViewPath(card.CardId, view.ViewId),
                view => $"{card.Title} — {view.Label}",
                (builder, _) => DashboardDefaults(builder).Group("View")));
        }

        return descriptors;
    }

    public static CommandDescriptor FieldFilter(DashboardFilterContext context, string filterName) =>
        CommandDescriptors.Describe($"dash.select.filter.{filterName}")
            .Path(FilterCommandPaths.FilterPath(filterName))
            .Help(DashboardFilterSlashLabels.FieldFilterHelp(context, filterName))
            .Group("Filter")
            .ArgTail($"picker:dash.field.{filterName}")
            .ArgHint(DashboardFilterSlashLabels.FieldFilterHint(context, filterName))
            .Surfaces(DashSurfaces)
            .Scope(DashSpecCommandScope.Dashboard)
            .Build();

    static CommandDescriptor DateFilter(DashboardFilterContext context, string filterName) =>
        CommandDescriptors.Describe(SelectDateFilterCommand.Id)
            .Path(FilterCommandPaths.FilterPath(filterName))
            .Help(DashboardFilterSlashLabels.DateFilterHelp(context, filterName))
            .Group("Filter")
            .ArgTail("picker+constructor:+date_today+date_week+date_month_week+date_month+date_quarter+date_range")
            .ArgHint(DashboardFilterSlashLabels.DateFilterHint(context, filterName))
            .ArgConstructors(DateConstructorBindings())
            .Surfaces(DashSurfaces)
            .Scope(DashSpecCommandScope.Dashboard)
            .Build();

    static CommandDescriptorBuilder DashboardDefaults(CommandDescriptorBuilder builder) =>
        builder.Group("Report")
            .Surfaces(DashSurfaces)
            .Scope(DashSpecCommandScope.Dashboard);

    static IReadOnlyList<ArgConstructorBinding> DateConstructorBindings() =>
    [
        new(DateConstructorCatalog.DateTodayId, "Сегодня", "Сегодняшний день"),
        new(DateConstructorCatalog.DateWeekId, "Неделя года…", "ISO-неделя в году (YYYY-Www)"),
        new(DateConstructorCatalog.DateMonthWeekId, "Неделя месяца…", "N-я 7-дневная неделя внутри месяца"),
        new(DateConstructorCatalog.DateMonthId, "Месяц…", "Календарный месяц"),
        new(DateConstructorCatalog.DateQuarterId, "Квартал…", "Квартал Q1–Q4"),
        new(DateConstructorCatalog.DateRangeId, "Период…", "Период с … по …"),
    ];
}
