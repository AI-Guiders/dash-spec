using System.Linq;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;
using Microsoft.FSharp.Core;
using FsharpCard = DashSpec.Modeling.Parse.Card;
using FsharpCardClick = DashSpec.Modeling.Parse.Card.CardClickEffect;
using FsharpDataSource = DashSpec.Modeling.Parse.DataSource;
using FsharpDiagram = DashSpec.Modeling.Parse.Diagram;
using FsharpDocument = DashSpec.Modeling.Parse.Document;
using FsharpFilter = DashSpec.Modeling.Parse.Filter;
using FsharpLayout = DashSpec.Modeling.Parse.Layout;
using FsharpPresentation = DashSpec.Modeling.Parse.Presentation;
using FsharpTooltip = DashSpec.Modeling.Parse.Tooltip;
using FsharpTransform = DashSpec.Modeling.Parse.Transform;

namespace DashSpec.Execution.Parsing;

/// <summary>Maps F# Modeling.Parse document IR to Core model types (ADR-0048 M4).</summary>
internal static class DocumentModelMapper
{
    internal static DashboardDocument ToCore(FsharpDocument.DashboardDocument document) =>
        new(
            document.Id,
            document.Title,
            FirstOrNull(document.ConnectorId),
            ToCore(document.SqlDialect),
            FirstOrNull(document.DiagramLibraryPath),
            FirstOrNull(document.PalettePath),
            FirstOrNull(document.ColorPalette),
            ToCore(document.Layout),
            ToCore(document.FiltersChrome),
            document.Filters.Select(ToCore).ToList(),
            document.DashboardFilters.ToList(),
            document.Tabs.Select(ToCore).ToList(),
            document.Cards.Select(ToCore).ToList(),
            MapOptional(document.ToolbarBoard, ToCore),
            MapOptional(document.ModuleExtensions, ToCore),
            ToDictionaryOrNull(document.ModuleDiagrams, ToCore),
            ToDictionaryOrNull(document.ModuleChartChromePresets, ToCore),
            ToDictionaryOrNull(document.ModuleTooltips, ToCoreTooltip),
            ToListOrNull(document.Pages)?.Select(ToCore).ToList(),
            ToDictionaryOrNull(document.CommandAliases, static x => x));

    private static TabDefinition ToCore(FsharpDocument.TabDefinition tab) =>
        new(
            tab.Id,
            FirstOrNull(tab.Label),
            tab.CardIds.ToList(),
            FirstOrNull(tab.DashspecPath),
            MapOptional(tab.LayoutBoard, ToCore));

    private static ReportPageDefinition ToCore(FsharpDocument.ReportPageDefinition page) =>
        new(
            page.Id,
            FirstOrNull(page.Title),
            MapOptional(page.LayoutBoard, ToCore),
            FirstOrNull(page.TabId),
            MapOptional(page.ToolbarBoard, ToCore),
            MapOptional(page.UsageDateDerive, ToCore));

    private static FilterDeriveDefinition ToCore(FsharpCard.FilterDeriveDefinition derive) =>
        new(
            derive.TargetFilter,
            derive.SourceFilter,
            FirstOrNull(derive.GrainFilterName));

    private static ModuleDiagramDefinition ToCore(FsharpDocument.ModuleDiagramDefinition module) =>
        new(
            ToCore(module.Diagram),
            MapOptional(module.Presentation, ToCore),
            MapOptional(module.SeriesTransform, ToCore),
            MapOptional(module.Inspect, ToCore),
            MapOptional(module.Tooltip, ToCoreTooltip));

    private static FilterDefinition ToCore(FsharpFilter.FilterDefinition filter) =>
        new(
            ToCore(filter.Kind),
            filter.Name,
            FirstOrNull(filter.DefaultExpression),
            FirstOrNull(filter.ColumnReference),
            FirstOrNull(filter.Label),
            FirstOrNull(filter.Widget),
            FirstOrNull(filter.MinValue),
            FirstOrNull(filter.MaxValue),
            FirstOrNull(filter.GrainFilterName),
            filter.SingleSelect,
            FirstOrNull(filter.LayoutRef),
            ToDictionaryOrNull(filter.GrainLabels, static x => x));

    private static FiltersChromeDefinition ToCore(FsharpFilter.FiltersChromeDefinition chrome) =>
        new(chrome.Layout, chrome.Sticky, chrome.Apply, chrome.DebounceMs);

    private static CardDefinition ToCore(FsharpCard.CardDefinition card) =>
        new(
            card.Id,
            card.Title,
            ToCore(card.Diagram),
            ToCore(card.DataSource),
            card.BoundFilters.ToList(),
            card.LocalFilters.ToList(),
            MapOptional(card.Placement, ToCore),
            FirstOrNull(card.TabId),
            FirstOrNull(card.LayoutRef),
            FirstOrNull(card.UseCardPreset),
            MapOptional(card.Legend, ToCore),
            MapOptional(card.Presentation, ToCore),
            MapOptional(card.SeriesTransform, ToCore),
            FirstOrNull(card.FilterHostCardId),
            ToListOrNull(card.HostedFilters),
            MapOptional(card.InteriorBoard, ToCore),
            FirstOrNull(card.DiagramSlotRef),
            MapOptional(card.ClickBehaviour, ToCore),
            card.ExtensionBlocks.Select(ToCore).ToList(),
            card.LocalFiltersManualApply,
            MapOptional(card.Visibility, ToCore),
            FirstOrNull(card.PhaseId),
            FirstOrNull(card.PageId),
            MapOptional(card.MatrixLimits, ToCore),
            FirstOrNull(card.OversizeMessage),
            MapOptional(card.Chrome, ToCore),
            MapOptional(card.Inspect, ToCore),
            MapOptional(card.Tooltip, ToCoreTooltip));

    private static LegendDefinition ToCore(FsharpCard.LegendDefinition legend) =>
        new(
            FirstOrNull(legend.MinLabel),
            FirstOrNull(legend.MaxLabel),
            FirstOrNull(legend.Title));

    private static CardClickBehaviour ToCore(FsharpCard.CardClickBehaviour behaviour) =>
        new(behaviour.Effects.Select(ToCore).ToList());

    private static CardClickEffect ToCore(FsharpCardClick effect) =>
        effect switch
        {
            FsharpCardClick.ShowSelection show => new ShowSelectionEffect(
                (ShowPlacement)(int)show.Item1,
                (ShowFormat)(int)show.Item2,
                (ShowSource)(int)show.Item3,
                show.Item4,
                FirstOrNull(show.Item5)),
            FsharpCardClick.SetFilterFromField set => new SetFilterFromFieldEffect(set.Item1, set.Item2),
            FsharpCardClick.InvokeHandler invoke => new InvokeHandlerEffect(
                invoke.Item1,
                invoke.Item2.ToDictionary(static x => x.Key, static x => x.Value, StringComparer.OrdinalIgnoreCase)),
            FsharpCardClick.GotoTab gotoTab => new GotoTabEffect(gotoTab.Item),
            FsharpCardClick.FocusPhase focus => new FocusPhaseEffect(focus.Item),
            FsharpCardClick.GotoPage page => new GotoPageEffect(page.Item),
            FsharpCardClick.GotoCatalogEntry catalog => new GotoCatalogEntryEffect(
                catalog.Item1,
                ToListOrNull(catalog.Item2)),
            _ => throw new ArgumentOutOfRangeException(nameof(effect), effect, "Unknown card click effect."),
        };

    private static CardVisibilityRule ToCore(FsharpCard.CardVisibilityRule rule) =>
        new(
            rule.FilterName,
            (CardVisibilityMode)(int)rule.Mode,
            FirstOrNull(rule.Message));

    private static CardChromeDefinition ToCore(FsharpCard.CardChromeDefinition chrome) =>
        new((CardBoundFilterChrome)(int)chrome.BoundFilters);

    private static MatrixRenderLimitsDefinition ToCore(FsharpCard.MatrixRenderLimitsDefinition limits) =>
        new(FirstOrNull(limits.MaxCells), FirstOrNull(limits.MaxAxisLabels));

    private static ExtensionBlockNode ToCore(FsharpCard.ExtensionBlockNode node) =>
        new(
            node.Keyword,
            node.Properties.ToDictionary(static x => x.Key, static x => x.Value, StringComparer.OrdinalIgnoreCase),
            node.Nested.Select(ToCore).ToList());

    private static ModuleExtensionsDefinition ToCore(FsharpCard.ModuleExtensionsDefinition extensions) =>
        new(
            extensions.EnabledPluginIds.ToList(),
            extensions.Imports.Select(static i => new ModuleExtensionImport(i.PluginId, FirstOrNull(i.AssemblyPath))).ToList());

    private static DiagramDefinition ToCore(FsharpDiagram.DiagramDefinition diagram) =>
        new(
            diagram.Kind,
            diagram.Properties.ToDictionary(static x => x.Key, static x => x.Value, StringComparer.OrdinalIgnoreCase),
            FirstOrNull(diagram.UsePreset));

    private static DataSourceDefinition ToCore(FsharpDataSource.DataSourceDefinition source) =>
        new(
            ToCore(source.Kind),
            source.Value,
            MapOptional(source.SqlCarrier, ToCore),
            FirstOrNull(source.Sheet));

    private static LayoutDefinition ToCore(FsharpLayout.LayoutDefinition layout) =>
        new(layout.Columns, layout.GapPx);

    private static PlacementDefinition ToCore(FsharpLayout.PlacementDefinition placement) =>
        new(placement.Row, placement.Col, placement.Span);

    private static LayoutBoardDefinition ToCore(FsharpLayout.LayoutBoardDefinition board)
    {
        var scopes = OptionModule.ToArray(board.ModuleScope);
        LayoutScope? moduleScope = scopes.Length > 0 ? MapScope(scopes[0]) : null;
        return new LayoutBoardDefinition(
            board.Rows.Select(static row => (IReadOnlyList<string>)row.ToList()).ToList(),
            moduleScope);
    }

    private static PresentationBlock ToCore(FsharpPresentation.PresentationBlock block) =>
        new(
            FirstOrNull(block.UsePreset),
            block.Properties.ToDictionary(static x => x.Key, static x => x.Value, StringComparer.OrdinalIgnoreCase));

    private static SeriesTransformBlock ToCore(FsharpTransform.SeriesTransformBlock transform) =>
        new(
            FirstOrNull(transform.UsePreset),
            FirstOrNull(transform.Max),
            FirstOrNull(transform.OtherLabel));

    private static InspectPresentation ToCore(FsharpDiagram.InspectPresentation inspect) =>
        new(
            FirstOrNull(inspect.TooltipId),
            FirstOrNull(inspect.Label),
            inspect.Format,
            inspect.Split);

    private static TooltipDefinition ToCoreTooltip(FsharpTooltip.TooltipDefinition tooltip)
    {
        var definition = new TooltipDefinition(
            tooltip.Id,
            tooltip.Variables.ToDictionary(static x => x.Key, static x => x.Value, StringComparer.OrdinalIgnoreCase),
            tooltip.Template);
        try
        {
            TooltipTemplate.Validate(definition);
        }
        catch (InvalidOperationException ex)
        {
            throw new DashSpecParseException(ex.Message);
        }

        return definition;
    }

    private static SqlDialect ToCore(FsharpDocument.SqlDialect dialect)
    {
        if (dialect.Equals(FsharpDocument.SqlDialect.TSql)) return SqlDialect.TSql;
        if (dialect.Equals(FsharpDocument.SqlDialect.Postgres)) return SqlDialect.Postgres;
        if (dialect.Equals(FsharpDocument.SqlDialect.Generic)) return SqlDialect.Generic;
        throw new ArgumentOutOfRangeException(nameof(dialect), dialect, "Unknown SQL dialect.");
    }

    private static FilterKind ToCore(FsharpFilter.FilterKind kind)
    {
        if (kind.Equals(FsharpFilter.FilterKind.Date)) return FilterKind.Date;
        if (kind.Equals(FsharpFilter.FilterKind.Field)) return FilterKind.Field;
        if (kind.Equals(FsharpFilter.FilterKind.Top)) return FilterKind.Top;
        throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown filter kind.");
    }

    private static DataSourceKind ToCore(FsharpDataSource.DataSourceKind kind)
    {
        if (kind.Equals(FsharpDataSource.DataSourceKind.View)) return DataSourceKind.View;
        if (kind.Equals(FsharpDataSource.DataSourceKind.Sql)) return DataSourceKind.Sql;
        if (kind.Equals(FsharpDataSource.DataSourceKind.Xlsx)) return DataSourceKind.Xlsx;
        throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown data source kind.");
    }

    private static DataSourceSqlCarrier ToCore(FsharpDataSource.DataSourceSqlCarrier carrier)
    {
        if (carrier.Equals(FsharpDataSource.DataSourceSqlCarrier.Query)) return DataSourceSqlCarrier.Query;
        if (carrier.Equals(FsharpDataSource.DataSourceSqlCarrier.File)) return DataSourceSqlCarrier.File;
        throw new ArgumentOutOfRangeException(nameof(carrier), carrier, "Unknown SQL carrier.");
    }

    private static LayoutScope? MapScope(FsharpLayout.LayoutScope scope)
    {
        if (scope.Equals(FsharpLayout.LayoutScope.Toolbar)) return LayoutScope.Toolbar;
        if (scope.Equals(FsharpLayout.LayoutScope.Tab)) return LayoutScope.Tab;
        if (scope.Equals(FsharpLayout.LayoutScope.Page)) return LayoutScope.Page;
        if (scope.Equals(FsharpLayout.LayoutScope.Card)) return LayoutScope.Card;
        return null;
    }

    private static T? FirstOrNull<T>(FSharpOption<T> option) where T : class
    {
        var items = OptionModule.ToArray(option);
        return items.Length > 0 ? items[0] : null;
    }

    private static int? FirstOrNull(FSharpOption<int> option)
    {
        var items = OptionModule.ToArray(option);
        return items.Length > 0 ? items[0] : null;
    }

    private static TCore? MapOptional<TFsharp, TCore>(FSharpOption<TFsharp> option, Func<TFsharp, TCore> map)
    {
        var items = OptionModule.ToArray(option);
        return items.Length > 0 ? map(items[0]) : default;
    }

    private static IReadOnlyList<T>? ToListOrNull<T>(FSharpOption<IReadOnlyList<T>> option)
    {
        var items = OptionModule.ToArray(option);
        return items.Length > 0 ? items[0].ToList() : null;
    }

    private static IReadOnlyDictionary<string, TCore>? ToDictionaryOrNull<TFsharp, TCore>(
        FSharpOption<IReadOnlyDictionary<string, TFsharp>> option,
        Func<TFsharp, TCore> map) =>
        ToDictionaryOrNull(option, (key, value) => map(value));

    private static IReadOnlyDictionary<string, TCore>? ToDictionaryOrNull<TFsharp, TCore>(
        FSharpOption<IReadOnlyDictionary<string, TFsharp>> option,
        Func<string, TFsharp, TCore> map)
    {
        var items = OptionModule.ToArray(option);
        if (items.Length == 0)
        {
            return null;
        }

        return items[0].ToDictionary(
            static x => x.Key,
            x => map(x.Key, x.Value),
            StringComparer.OrdinalIgnoreCase);
    }
}
