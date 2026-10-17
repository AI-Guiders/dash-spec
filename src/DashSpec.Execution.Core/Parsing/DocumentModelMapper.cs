using System.Linq;
using DashSpec.Abstractions.Data;
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
using FsharpPalette = DashSpec.Modeling.Parse.Palette;
using FsharpPresentation = DashSpec.Modeling.Parse.Presentation;
using FsharpTooltip = DashSpec.Modeling.Parse.Tooltip;
using FsharpTransform = DashSpec.Modeling.Parse.Transform;
using FsharpTypes = DashSpec.Modeling.Parse.Types.RowTypeSchemaBridge;
using FsharpDataFlow = DashSpec.Modeling.Parse.DataFlow;

namespace DashSpec.Execution.Parsing;

/// <summary>Maps F# Modeling.Parse document IR to Core model types (ADR-0048 M4).</summary>
internal static class DocumentModelMapper
{
    internal static DashboardDocument ToCore(FsharpDocument.DashboardDocument document) =>
        new(
            document.Id,
            document.Title,
            FirstOrNull(document.ConnectorId),
            document.SqlDialect,
            FirstOrNull(document.DiagramLibraryPath),
            MapOptional(document.Palette, ToCore),
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
            ToDictionaryOrNull(document.CommandAliases, static x => x),
            ToCore(document.FormatDefaults),
            MapOptional(document.CardsChrome, ToCore),
            MapOptional(document.TimePolicy, static policy => policy),
            ToRowTypeSchemas(document.ResolvedRowTypes),
            FirstOrNull(document.DashflowPath),
            MapOptional(document.Dashflow, ToCoreDashflow));

    private static PaletteDefinition ToCore(FsharpPalette.PaletteDocument palette) =>
        new(
            palette.Id,
            palette.Properties.ToDictionary(static x => x.Key, static x => x.Value, StringComparer.OrdinalIgnoreCase));

    private static ReportFormatDefaults ToCore(FsharpDocument.ReportFormatDefaults defaults) =>
        defaults;

    private static TabDefinition ToCore(FsharpDocument.TabDefinition tab) =>
        tab;

    private static ReportPageDefinition ToCore(FsharpDocument.ReportPageDefinition page) =>
        page;

    private static FilterDeriveDefinition ToCore(FsharpCard.FilterDeriveDefinition derive) =>
        derive;

    private static ModuleDiagramDefinition ToCore(FsharpDocument.ModuleDiagramDefinition module) =>
        module;

    private static FilterDefinition ToCore(FsharpFilter.FilterDefinition filter) =>
        filter;


    private static FiltersChromeDefinition ToCore(FsharpFilter.FiltersChromeDefinition chrome) =>
        chrome;

    private static CardDefinition ToCore(FsharpCard.CardDefinition card) =>
        card;

    private static CardDiagramSlotDefinition ToCore(FsharpCard.CardDiagramSlot slot) =>
        slot;

    private static LegendDefinition ToCore(FsharpCard.LegendDefinition legend) =>
        legend;

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
            FsharpCardClick.DrillTableFromCell drill => new DrillTableFromCellEffect(
                drill.Item.Select(static b => new SetFilterFromFieldEffect(b.Item1, b.Item2)).ToList()),
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
            rule.Mode,
            rule.Message);

    private static CardsChromeDefinition ToCore(FsharpCard.CardsChromeDefinition chrome) =>
        new((CardsFoldPolicy)(int)chrome.FoldPolicy);

    private static CardChromeDefinition ToCore(FsharpCard.CardChromeDefinition chrome) =>
        new((CardBoundFilterChrome)(int)chrome.BoundFilters, chrome.HideTitle, (CardFoldMode)(int)chrome.Fold);

    private static MatrixRenderLimitsDefinition ToCore(FsharpCard.MatrixRenderLimitsDefinition limits) =>
        limits;

    private static ExtensionBlockNode ToCore(FsharpCard.ExtensionBlockNode node) =>
        new(
            node.Keyword,
            node.Properties.ToDictionary(static x => x.Key, static x => x.Value, StringComparer.OrdinalIgnoreCase),
            node.Nested.Select(ToCore).ToList());

    private static ModuleExtensionsDefinition ToCore(FsharpCard.ModuleExtensionsDefinition extensions) =>
        extensions;

    private static DiagramDefinition ToCore(FsharpDiagram.DiagramDefinition diagram) =>
        diagram;

    private static DataSourceDefinition ToCore(FsharpDataSource.DataSourceDefinition source) =>
        source;


    private static IReadOnlyDictionary<string, RowTypeSchema> ToRowTypeSchemas(
        IReadOnlyDictionary<string, DashSpec.Modeling.Core.RowTypeDef> types)
    {
        if (types.Count == 0)
        {
            return DashboardDocument.EmptyRowTypeSchemas;
        }

        return FsharpTypes.toCatalog(types.Values);
    }

    private static LayoutDefinition ToCore(FsharpLayout.LayoutDefinition layout) =>
        new(layout.Columns, layout.GapPx);

    private static PlacementDefinition ToCore(FsharpLayout.PlacementDefinition placement) =>
        new(placement.Row, placement.Col, placement.Span);

    private static LayoutBoardDefinition ToCore(FsharpLayout.LayoutBoardDefinition board) =>
        board;

    private static PresentationBlock ToCore(FsharpPresentation.PresentationBlock block) =>
        block;

    private static SeriesTransformBlock ToCore(FsharpTransform.SeriesTransformBlock transform) =>
        transform;

    private static InspectPresentation ToCore(FsharpDiagram.InspectPresentation inspect) =>
        inspect;

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

    private static FilterKind ToCore(FsharpFilter.FilterKind kind)
    {
        if (kind.Equals(FsharpFilter.FilterKind.Date)) return FilterKind.Date;
        if (kind.Equals(FsharpFilter.FilterKind.Field)) return FilterKind.Field;
        if (kind.Equals(FsharpFilter.FilterKind.Top)) return FilterKind.Top;
        throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown filter kind.");
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

    private static bool? FirstOrNull(FSharpOption<bool> option)
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

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? ToNestedFilterDefaultsOrNull(
        FSharpOption<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> option)
    {
        var items = OptionModule.ToArray(option);
        if (items.Length == 0)
        {
            return null;
        }

        return items[0].ToDictionary(
            static x => x.Key,
            static x => (IReadOnlyDictionary<string, string>)x.Value.ToDictionary(
                static y => y.Key,
                static y => y.Value,
                StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }

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

    private static IReadOnlyDictionary<string, string>? ToDisplayBindingsOrNull(
        FSharpOption<IReadOnlyDictionary<string, string>> option)
    {
        var items = OptionModule.ToArray(option);
        if (items.Length == 0 || items[0].Count == 0)
        {
            return null;
        }

        return items[0].ToDictionary(
            static x => x.Key,
            static x => x.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static CardFlowInputDefinition ToCoreFlowInput(FsharpCard.CardFlowInput input) =>
        new(input.Alias, input.NodeId, input.PortName);

    private static DashflowModuleDefinition ToCoreDashflow(FsharpDataFlow.DashflowModule module) =>
        new(
            module.FlowId,
            module.Sources.Select(ToCoreDashflowSource).ToList(),
            module.Transformers.Select(ToCoreDashflowTransformer).ToList(),
            module.Links.Select(ToCoreDashflowLink).ToList());

    private static DashflowSourceDefinition ToCoreDashflowSource(FsharpDataFlow.DashflowSourceDef source)
    {
        var (providerInfer, providerId) = ToCoreProvider(source.Provider);
        return new(
            source.Id,
            providerInfer,
            providerId,
            ToCoreSourceFromKind(source.From.Kind),
            source.From.Value,
            source.OutputPort,
            source.OutputRowType);
    }

    private static (bool Infer, string? Id) ToCoreProvider(FsharpDataFlow.DashflowProviderBinding binding) =>
        (
            FsharpDataFlow.DashflowProviderBridge.isInfer(binding),
            MapOptional(FsharpDataFlow.DashflowProviderBridge.tryNamedId(binding), static x => x));

    private static DashflowSourceFromKind ToCoreSourceFromKind(FsharpDataFlow.SourceFromKind kind)
    {
        if (kind.Equals(FsharpDataFlow.SourceFromKind.View))
        {
            return DashflowSourceFromKind.View;
        }

        if (kind.Equals(FsharpDataFlow.SourceFromKind.SqlQuery))
        {
            return DashflowSourceFromKind.SqlQuery;
        }

        return DashflowSourceFromKind.SqlFile;
    }

    private static DashflowTransformerDefinition ToCoreDashflowTransformer(FsharpDataFlow.DashflowTransformerDef transformer) =>
        new(
            transformer.Id,
            transformer.Outputs.Select(static x => new DashflowTransformerPortDefinition(x.Item1, x.Item2)).ToList(),
            FirstOrNull(transformer.DefaultInputPort),
            FirstOrNull(transformer.DefaultOutputPort),
            transformer.Steps.Select(ToCoreTransformStep).ToList());

    private static DashflowTransformStepDefinition ToCoreTransformStep(FsharpDataFlow.DashflowTransformStepDef step) =>
        new(
            step.PluginId,
            step.Parameters.Select(static p => new DashflowTransformParameterDefinition(p.Key, p.Value)).ToList());

    private static DashflowLinkDefinition ToCoreDashflowLink(FsharpDataFlow.FlowLinkDef link) =>
        link;
}
