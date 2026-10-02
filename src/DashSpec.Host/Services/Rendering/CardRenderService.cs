using System.Globalization;
using DashSpec.Abstractions.Connectors;
using DashSpec.Execution.Compilation;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;
using DashSpec.Execution.Runtime;
using DashSpec.Host.Plugins;
using DashSpec.Host.Services.Abstractions;
using DashSpec.Viz;
using DashSpec.Host.Services.Presentation;
using DashSpec.Core.Layout;
using DashSpec.Core.Resolution;
using DashSpec.Host.Configuration;

namespace DashSpec.Host.Services.Rendering;

public sealed class CardRenderService(
    VizPluginRegistry vizPlugins,
    ICardCellDrillState cellDrill,
    DashSpecTomlRoot bootstrap) : ICardRenderer
{
    public async Task<CardRenderResult> RenderAsync(
        CardDefinition card,
        DashboardDocument document,
        FilterState filters,
        IReadOnlyDictionary<string, FilterDefinition> filterIndex,
        SpecLibrary? library,
        IDataSourceConnector connector,
        string? specDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(filters);
        ArgumentNullException.ThrowIfNull(filterIndex);
        ArgumentNullException.ThrowIfNull(connector);

        LabelFormat.SetReportDefaults(document.ResolvedFormatDefaults);
        try
        {
            return await RenderCoreAsync(
                card,
                document,
                filters,
                filterIndex,
                library,
                connector,
                specDirectory,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            LabelFormat.ClearReportDefaults();
        }
    }

    public async Task<IReadOnlyDictionary<string, CardSlotRenderResult>> RenderInteriorSlotsAsync(
        CardDefinition card,
        DashboardDocument document,
        FilterState filters,
        IReadOnlyDictionary<string, FilterDefinition> filterIndex,
        SpecLibrary? library,
        IDataSourceConnector connector,
        string? specDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(filters);
        ArgumentNullException.ThrowIfNull(filterIndex);
        ArgumentNullException.ThrowIfNull(connector);

        LabelFormat.SetReportDefaults(document.ResolvedFormatDefaults);
        try
        {
            var resolved = CardResolver.Resolve(card, library, document.DashboardFilters);
            var effective = resolved.Card;
            var primarySlotRef = CardDiagramSlotCatalog.ResolvePrimarySlotRef(effective);
            return await RenderSecondarySlotsAsync(
                effective,
                document,
                filters,
                filterIndex,
                library,
                connector,
                specDirectory,
                primarySlotRef,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            LabelFormat.ClearReportDefaults();
        }
    }

    private async Task<CardRenderResult> RenderCoreAsync(
        CardDefinition card,
        DashboardDocument document,
        FilterState filters,
        IReadOnlyDictionary<string, FilterDefinition> filterIndex,
        SpecLibrary? library,
        IDataSourceConnector connector,
        string? specDirectory,
        CancellationToken cancellationToken)
    {
        var resolved = CardResolver.Resolve(card, library, document.DashboardFilters);
        var effective = resolved.Card;
        var reportTime = ResolveReportTime(document);
        var query = QueryCompiler.Compile(
            effective,
            filters,
            filterIndex,
            document,
            document.SqlDialect,
            specDirectory,
            reportTimePolicy: reportTime);
        var rows = await connector.QueryAsync(query, cancellationToken).ConfigureAwait(false);
        var kind = DiagramKindRegistry.Resolve(effective.Diagram.Kind);
        var chartPresentation = kind.DataFamily is DiagramDataFamily.Chart
            ? CompositionResolution.ResolveChartPresentation(effective, library)
            : null;
        var seriesTransform = kind.DataFamily is DiagramDataFamily.Chart or DiagramDataFamily.Matrix
            ? CompositionResolution.ResolveSeriesTransform(effective, library)
            : null;
        var matrixPresentation = kind.DataFamily is DiagramDataFamily.Matrix
            ? MatrixPresentation.FromCard(effective, library)
            : null;
        var tablePresentation = kind.DataFamily is DiagramDataFamily.Table
            ? CompositionResolution.ResolveTablePresentation(effective, library)
            : null;
        var renderPluginId = vizPlugins.Resolve(resolved.RenderPluginId, kind.DataFamily);
        var interiorPlacements = DashboardLayoutHelper.ResolveInteriorPlacements(card, document);
        var localFilterChromePlacements = CardLocalFilterChromeCompactor.Compact(
            card,
            document.Filters,
            document.Layout.Columns);
        var primarySlotRef = CardDiagramSlotCatalog.ResolvePrimarySlotRef(card);

        var (filterLinkHint, filterLinkCssClass) = CardFilterLinkHints.Resolve(card, document);
        var topFilterScopeHint = CardFilterScopeHints.ResolveTopFilterScope(card, document);
        var interiorSlotRenders = await RenderSecondarySlotsAsync(
            card,
            document,
            filters,
            filterIndex,
            library,
            connector,
            specDirectory,
            primarySlotRef,
            cancellationToken).ConfigureAwait(false);

        CardRenderResult AttachSlots(CardRenderResult result)
        {
            result = result with { LocalFilterChromePlacements = localFilterChromePlacements };
            return interiorSlotRenders.Count == 0
                ? result
                : result with { InteriorSlotRenders = interiorSlotRenders };
        }

        if (kind.DataFamily is DiagramDataFamily.Scalar)
        {
            string? numberDelta = null;
            string? numberDeltaTone = null;
            if (KpiPriorPeriod.WantsPriorDelta(effective.Diagram) &&
                KpiPriorPeriod.TryBuildPriorFilters(
                    EnumerateCardFilters(card),
                    filters,
                    filterIndex,
                    out var priorFilters) &&
                KpiPriorPeriod.TryReadScalar(rows, effective.Diagram, out var currentValue))
            {
                var priorQuery = QueryCompiler.Compile(
                    effective,
                    priorFilters,
                    filterIndex,
                    document,
                    document.SqlDialect,
                    specDirectory,
                    reportTimePolicy: reportTime);
                var priorRows = await connector.QueryAsync(priorQuery, cancellationToken).ConfigureAwait(false);
                if (KpiPriorPeriod.TryReadScalar(priorRows, effective.Diagram, out var priorValue))
                {
                    (numberDelta, numberDeltaTone) = KpiPriorPeriod.FormatDelta(currentValue, priorValue);
                }
            }

            return AttachSlots(new CardRenderResult(
                card.Id,
                card.Title,
                effective.Diagram.Kind,
                kind.DataFamily,
                renderPluginId,
                Number: FormatNumber(rows, effective.Diagram),
                NumberDelta: numberDelta,
                NumberDeltaTone: numberDeltaTone,
                Placement: card.Placement,
                InteriorPlacements: interiorPlacements,
                BoundFilters: card.BoundFilters,
                LocalFilters: card.LocalFilters,
                ClickBehaviour: card.ClickBehaviour,
                ExtensionBlocks: card.ExtensionBlocks,
                LocalFiltersManualApply: card.LocalFiltersManualApply,
                LocalFiltersApplySplitIndex: card.LocalFiltersApplySplitIndex,
                MatrixLimits: card.MatrixLimits,
                OversizeMessage: card.OversizeMessage));
        }

        return AttachSlots(kind.DataFamily switch
        {
            DiagramDataFamily.Chart =>
                new CardRenderResult(
                    card.Id,
                    card.Title,
                    effective.Diagram.Kind,
                    kind.DataFamily,
                    renderPluginId,
                    Chart: ChartDataBuilder.BuildChart(rows, effective.Diagram, seriesTransform, effective, library, document.ColorPalette),
                    DetailChart: seriesTransform is null
                        ? null
                        : ChartDataBuilder.BuildChart(rows, effective.Diagram, null, effective, library, document.ColorPalette),
                    Placement: card.Placement,
                    InteriorPlacements: interiorPlacements,
                    ChartPresentation: chartPresentation,
                    BoundFilters: card.BoundFilters,
                    LocalFilters: card.LocalFilters,
                    ClickBehaviour: card.ClickBehaviour,
                    ExtensionBlocks: card.ExtensionBlocks,
                    LocalFiltersManualApply: card.LocalFiltersManualApply,
                LocalFiltersApplySplitIndex: card.LocalFiltersApplySplitIndex,
                    MatrixLimits: card.MatrixLimits,
                    OversizeMessage: card.OversizeMessage,
                    FilterLinkHint: filterLinkHint,
                    FilterLinkCssClass: filterLinkCssClass,
                    TopFilterScopeHint: topFilterScopeHint),
            DiagramDataFamily.Table =>
                new CardRenderResult(
                    card.Id,
                    card.Title,
                    effective.Diagram.Kind,
                    kind.DataFamily,
                    renderPluginId,
                    Table: ChartDataBuilder.BuildTable(rows, effective.Diagram),
                    Placement: card.Placement,
                    InteriorPlacements: interiorPlacements,
                    TablePresentation: tablePresentation,
                    BoundFilters: card.BoundFilters,
                    LocalFilters: card.LocalFilters,
                    ClickBehaviour: card.ClickBehaviour,
                    ExtensionBlocks: card.ExtensionBlocks,
                    LocalFiltersManualApply: card.LocalFiltersManualApply,
                LocalFiltersApplySplitIndex: card.LocalFiltersApplySplitIndex,
                    MatrixLimits: card.MatrixLimits,
                    OversizeMessage: card.OversizeMessage,
                    FilterLinkHint: filterLinkHint,
                    FilterLinkCssClass: filterLinkCssClass,
                    TopFilterScopeHint: topFilterScopeHint),
            DiagramDataFamily.Matrix =>
                new CardRenderResult(
                    card.Id,
                    card.Title,
                    effective.Diagram.Kind,
                    kind.DataFamily,
                    renderPluginId,
                    Matrix: ChartDataBuilder.BuildHeatmap(rows, effective.Diagram, seriesTransform, effective.Tooltip),
                    DetailMatrix: seriesTransform is null
                        ? null
                        : ChartDataBuilder.BuildHeatmap(rows, effective.Diagram, null, effective.Tooltip),
                    Placement: card.Placement,
                    InteriorPlacements: interiorPlacements,
                    MatrixPresentation: matrixPresentation,
                    BoundFilters: card.BoundFilters,
                    LocalFilters: card.LocalFilters,
                    ClickBehaviour: card.ClickBehaviour,
                    ExtensionBlocks: card.ExtensionBlocks,
                    LocalFiltersManualApply: card.LocalFiltersManualApply,
                LocalFiltersApplySplitIndex: card.LocalFiltersApplySplitIndex,
                    MatrixLimits: card.MatrixLimits,
                    OversizeMessage: card.OversizeMessage,
                    FilterLinkHint: filterLinkHint,
                    FilterLinkCssClass: filterLinkCssClass,
                    TopFilterScopeHint: topFilterScopeHint),
            DiagramDataFamily.Gantt =>
                new CardRenderResult(
                    card.Id,
                    card.Title,
                    effective.Diagram.Kind,
                    kind.DataFamily,
                    renderPluginId,
                    Gantt: EnrichGanttPayload(
                        ChartDataBuilder.BuildGantt(rows, effective.Diagram),
                        effective,
                        library),
                    Placement: card.Placement,
                    InteriorPlacements: interiorPlacements,
                    BoundFilters: card.BoundFilters,
                    LocalFilters: card.LocalFilters,
                    ClickBehaviour: card.ClickBehaviour,
                    ExtensionBlocks: card.ExtensionBlocks,
                    LocalFiltersManualApply: card.LocalFiltersManualApply,
                LocalFiltersApplySplitIndex: card.LocalFiltersApplySplitIndex,
                    MatrixLimits: card.MatrixLimits,
                    OversizeMessage: card.OversizeMessage,
                    FilterLinkHint: filterLinkHint,
                    FilterLinkCssClass: filterLinkCssClass,
                    TopFilterScopeHint: topFilterScopeHint),
            _ => throw new ArgumentOutOfRangeException(nameof(card)),
        });
    }

    private async Task<IReadOnlyDictionary<string, CardSlotRenderResult>> RenderSecondarySlotsAsync(
        CardDefinition card,
        DashboardDocument document,
        FilterState filters,
        IReadOnlyDictionary<string, FilterDefinition> filterIndex,
        SpecLibrary? library,
        IDataSourceConnector connector,
        string? specDirectory,
        string primarySlotRef,
        CancellationToken cancellationToken)
    {
        var slots = CardDiagramSlotCatalog.ResolveSlots(card);
        if (slots.Count <= 1)
        {
            return new Dictionary<string, CardSlotRenderResult>(StringComparer.OrdinalIgnoreCase);
        }

        var renders = new Dictionary<string, CardSlotRenderResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var (slotRef, slot) in slots)
        {
            if (string.Equals(slotRef, primarySlotRef, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var slotCard = card with
            {
                Diagram = slot.Diagram,
                DataSource = slot.DataSource,
                BoundFilters = slot.BoundFilters,
                Legend = slot.Legend,
                Presentation = slot.Presentation,
                SeriesTransform = slot.SeriesTransform,
            };

            try
            {
                var resolved = CardResolver.Resolve(slotCard, library, document.DashboardFilters);
                var effective = resolved.Card;
                var drillOverlay = cellDrill.Get(card.Id);
                var query = QueryCompiler.Compile(
                    effective,
                    filters,
                    filterIndex,
                    document,
                    document.SqlDialect,
                    specDirectory,
                    drillOverlay,
                    ResolveReportTime(document));
                var rows = await connector.QueryAsync(query, cancellationToken).ConfigureAwait(false);
                var kind = DiagramKindRegistry.Resolve(effective.Diagram.Kind);
                var renderPluginId = vizPlugins.Resolve(resolved.RenderPluginId, kind.DataFamily);
                var matrixPresentation = kind.DataFamily is DiagramDataFamily.Matrix
                    ? MatrixPresentation.FromCard(effective, library)
                    : null;
                var tablePresentation = kind.DataFamily is DiagramDataFamily.Table
                    ? CompositionResolution.ResolveTablePresentation(effective, library)
                    : null;
                var seriesTransform = kind.DataFamily is DiagramDataFamily.Chart or DiagramDataFamily.Matrix
                    ? CompositionResolution.ResolveSeriesTransform(effective, library)
                    : null;

                renders[slotRef] = kind.DataFamily switch
                {
                    DiagramDataFamily.Table => new CardSlotRenderResult(
                        slotRef,
                        effective.Diagram.Kind,
                        kind.DataFamily,
                        renderPluginId,
                        Table: ChartDataBuilder.BuildTable(rows, effective.Diagram),
                        TablePresentation: tablePresentation),
                    DiagramDataFamily.Matrix => new CardSlotRenderResult(
                        slotRef,
                        effective.Diagram.Kind,
                        kind.DataFamily,
                        renderPluginId,
                        Matrix: ChartDataBuilder.BuildHeatmap(rows, effective.Diagram, seriesTransform, effective.Tooltip),
                        MatrixPresentation: matrixPresentation),
                    DiagramDataFamily.Chart => new CardSlotRenderResult(
                        slotRef,
                        effective.Diagram.Kind,
                        kind.DataFamily,
                        renderPluginId,
                        Chart: ChartDataBuilder.BuildChart(
                            rows,
                            effective.Diagram,
                            seriesTransform,
                            effective,
                            library,
                            document.ColorPalette)),
                    DiagramDataFamily.Gantt => new CardSlotRenderResult(
                        slotRef,
                        effective.Diagram.Kind,
                        kind.DataFamily,
                        renderPluginId,
                        Gantt: EnrichGanttPayload(
                            ChartDataBuilder.BuildGantt(rows, effective.Diagram),
                            effective,
                            library)),
                    _ => new CardSlotRenderResult(
                        slotRef,
                        effective.Diagram.Kind,
                        kind.DataFamily,
                        renderPluginId,
                        Error: $"Unsupported interior slot family '{kind.DataFamily}'."),
                };
            }
            catch (Exception ex)
            {
                renders[slotRef] = new CardSlotRenderResult(
                    slotRef,
                    slot.Diagram.Kind,
                    DiagramDataFamily.Table,
                    string.Empty,
                    Error: ex.Message);
            }
        }

        return renders;
    }

    private static IEnumerable<string> EnumerateCardFilters(CardDefinition card)
    {
        foreach (var name in card.BoundFilters)
        {
            yield return name;
        }

        foreach (var name in card.LocalFilters)
        {
            yield return name;
        }
    }

    private static string? FormatNumber(
        DashSpec.Abstractions.Data.TypedRowBatch rows,
        DiagramDefinition diagram)
    {
        if (rows.IsEmpty)
        {
            return null;
        }

        var value = rows[0].GetClr(DiagramBindings.Column(diagram, "value"));
        if (value is null or DBNull)
        {
            return null;
        }

        return value switch
        {
            DateOnly => LabelFormat.FormatObject(value, LabelFormat.ResolveDateFormat(null)),
            DateTime dt => LabelFormat.FormatObject(
                value,
                dt.TimeOfDay == TimeSpan.Zero
                    ? LabelFormat.ResolveDateFormat(null)
                    : LabelFormat.ResolveDateTimeFormat(null)),
            _ => FormatScalarMeasure(value, diagram),
        };
    }

    private static string FormatScalarMeasure(object value, DiagramDefinition diagram)
    {
        var culture = CultureInfo.CurrentCulture;
        var preferInteger =
            diagram.Properties.TryGetValue("scale_value", out var scale) &&
            scale.Equals("integer", StringComparison.OrdinalIgnoreCase);

        if (preferInteger)
        {
            return value switch
            {
                IFormattable formattable => formattable.ToString("N0", culture) ?? "—",
                _ => Convert.ToString(value, culture) ?? "—",
            };
        }

        return value switch
        {
            byte or sbyte or short or ushort or int or uint or long or ulong =>
                ((IFormattable)value).ToString("N0", culture) ?? "—",
            decimal d when d == decimal.Truncate(d) => d.ToString("N0", culture),
            double d when Math.Abs(d % 1) < 1e-9 => d.ToString("N0", culture),
            float f when Math.Abs(f % 1) < 1e-6f => f.ToString("N0", culture),
            IFormattable formattable => formattable.ToString(null, culture) ?? "—",
            _ => Convert.ToString(value, culture) ?? "—",
        };
    }

    private static GanttPayload EnrichGanttPayload(
        GanttPayload payload,
        CardDefinition card,
        SpecLibrary? library) =>
        payload with
        {
            VisibleRows = CompositionResolution.ResolveVisibleRows(card, library),
        };

    private ReportTimePolicy ResolveReportTime(DashboardDocument document) =>
        ReportTimePolicyResolver.ResolveEffective(
            document.TimePolicy,
            bootstrap.ReportTime.ToSettingsDictionary(),
            LabelFormat.DisplayTimeZone?.Id);
}
