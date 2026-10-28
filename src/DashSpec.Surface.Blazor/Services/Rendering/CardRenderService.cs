using System.Globalization;
using DashSpec.Abstractions.Connectors;
using DashSpec.Execution.Compilation;
using DashSpec.Execution.Runtime;
using DashSpec.Modeling.Parse.Card;
using DashSpec.Viewer.Plugins;
using DashSpec.Abstractions.Hosting;
using DashSpec.Viz;
using DashSpec.Viz.Platform;
using DashSpec.Surface.Blazor.Services.Presentation;

namespace DashSpec.Surface.Blazor.Services.Rendering;

public sealed class CardRenderService(
    VizPluginRegistry vizPlugins,
    ICardCellDrillState cellDrill,
    IViewerRuntimeContext viewerContext) : ICardRenderer
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
            var flowPath = ResolveFlowPath(document, effective);
            return await RenderSecondarySlotsAsync(
                effective,
                document,
                filters,
                filterIndex,
                library,
                connector,
                specDirectory,
                primarySlotRef,
                flowPath,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            LabelFormat.ClearReportDefaults();
        }
    }

    /// <summary>Граф (G) получения данных карточки, если у неё есть flow-вход; иначе null.</summary>
    private static DashflowExecutionPath? ResolveFlowPath(DashboardDocument document, CardDefinition effective)
    {
        if (document.Dashflow is null || effective.FlowInput?.Value is null)
        {
            return null;
        }

        try
        {
            return FlowCardExecution.ResolvePath(document, effective.FlowInput.Value);
        }
        catch (DashSpecParseException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
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
        var effective = DocumentFlowBinder.MaterializeCard(resolved.Card, document);
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
        rows = DashflowCardDataPipeline.ApplyTransforms(document, effective, rows);
        var flowPath = ResolveFlowPath(document, effective);
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
        var renderPluginId = vizPlugins.Resolve(kind.DataFamily, kind.Id);
        var interiorPlacements = DashboardLayoutHelper.ResolveInteriorPlacements(card, document);
        var localFilterChromePlacements = CardLocalFilterChromeCompactor.Compact(
            card,
            document.Filters,
            document.Layout.Columns);
        var primarySlotRef = CardDiagramSlotCatalog.ResolvePrimarySlotRef(card);

        var (filterLinkHint, filterLinkCssClass) = CardFilterLinkHints.Resolve(card, document);
        var topFilterScopeHint = CardFilterScopeHints.ResolveTopFilterScope(card, document);
        var interiorSlotRenders = await RenderSecondarySlotsAsync(
            effective,
            document,
            filters,
            filterIndex,
            library,
            connector,
            specDirectory,
            primarySlotRef,
            flowPath,
            cancellationToken).ConfigureAwait(false);

        CardRenderResult AttachSlots(CardRenderResult result)
        {
            // Present-layer formatting (ADR-0079 §3): the report defaults ride the render
            // result — components format outside the payload-building async flow.
            result = result with
            {
                LocalFilterChromePlacements = localFilterChromePlacements,
                FormatDefaults = document.ResolvedFormatDefaults,
                FlowPath = flowPath,
            };
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
                priorRows = DashflowCardDataPipeline.ApplyTransforms(document, effective, priorRows);
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
                Placement: card.Placement?.Value,
                InteriorPlacements: interiorPlacements,
                BoundFilters: card.BoundFilters,
                LocalFilters: card.LocalFilters,
                ClickBehaviour: card.ClickBehaviour?.Value,
                ClickHandlers: card.ClickHandlers,
                ExtensionBlocks: card.ExtensionBlocks,
                LocalFiltersManualApply: card.LocalFiltersManualApply,
                LocalFiltersApplySplitIndex: card.LocalFiltersApplySplitIndex?.Value,
                MatrixLimits: card.MatrixLimits?.Value,
                OversizeMessage: card.OversizeMessage?.Value));
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
                    Chart: ChartDataBuilder.BuildChart(rows, effective.Diagram, seriesTransform, effective, library, document.ColorPalette?.Value),
                    DetailChart: seriesTransform is null
                        ? null
                        : ChartDataBuilder.BuildChart(rows, effective.Diagram, null, effective, library, document.ColorPalette?.Value),
                    Placement: card.Placement?.Value,
                    InteriorPlacements: interiorPlacements,
                    ChartPresentation: chartPresentation,
                    BoundFilters: card.BoundFilters,
                    LocalFilters: card.LocalFilters,
                    ClickBehaviour: card.ClickBehaviour?.Value,
                    ClickHandlers: card.ClickHandlers,
                    ExtensionBlocks: card.ExtensionBlocks,
                    LocalFiltersManualApply: card.LocalFiltersManualApply,
                LocalFiltersApplySplitIndex: card.LocalFiltersApplySplitIndex?.Value,
                    MatrixLimits: card.MatrixLimits?.Value,
                    OversizeMessage: card.OversizeMessage?.Value,
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
                    Placement: card.Placement?.Value,
                    InteriorPlacements: interiorPlacements,
                    TablePresentation: tablePresentation,
                    BoundFilters: card.BoundFilters,
                    LocalFilters: card.LocalFilters,
                    ClickBehaviour: card.ClickBehaviour?.Value,
                    ClickHandlers: card.ClickHandlers,
                    ExtensionBlocks: card.ExtensionBlocks,
                    LocalFiltersManualApply: card.LocalFiltersManualApply,
                LocalFiltersApplySplitIndex: card.LocalFiltersApplySplitIndex?.Value,
                    MatrixLimits: card.MatrixLimits?.Value,
                    OversizeMessage: card.OversizeMessage?.Value,
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
                    Matrix: ChartDataBuilder.BuildHeatmap(rows, effective.Diagram, seriesTransform, effective.Tooltip?.Value),
                    DetailMatrix: seriesTransform is null
                        ? null
                        : ChartDataBuilder.BuildHeatmap(rows, effective.Diagram, null, effective.Tooltip?.Value),
                    Placement: card.Placement?.Value,
                    InteriorPlacements: interiorPlacements,
                    MatrixPresentation: matrixPresentation,
                    BoundFilters: card.BoundFilters,
                    LocalFilters: card.LocalFilters,
                    ClickBehaviour: card.ClickBehaviour?.Value,
                    ClickHandlers: card.ClickHandlers,
                    ExtensionBlocks: card.ExtensionBlocks,
                    LocalFiltersManualApply: card.LocalFiltersManualApply,
                LocalFiltersApplySplitIndex: card.LocalFiltersApplySplitIndex?.Value,
                    MatrixLimits: card.MatrixLimits?.Value,
                    OversizeMessage: card.OversizeMessage?.Value,
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
                    Placement: card.Placement?.Value,
                    InteriorPlacements: interiorPlacements,
                    BoundFilters: card.BoundFilters,
                    LocalFilters: card.LocalFilters,
                    ClickBehaviour: card.ClickBehaviour?.Value,
                    ClickHandlers: card.ClickHandlers,
                    ExtensionBlocks: card.ExtensionBlocks,
                    LocalFiltersManualApply: card.LocalFiltersManualApply,
                LocalFiltersApplySplitIndex: card.LocalFiltersApplySplitIndex?.Value,
                    MatrixLimits: card.MatrixLimits?.Value,
                    OversizeMessage: card.OversizeMessage?.Value,
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
        DashflowExecutionPath? flowPath,
        CancellationToken cancellationToken)
    {
        var slots = CardDiagramSlotCatalog.ResolveSlots(card);
        if (slots.Count <= 1)
        {
            return new Dictionary<string, CardSlotRenderResult>(StringComparer.OrdinalIgnoreCase);
        }

        var drillTargets = CardDrillSlotTargets.Resolve(card);
        var renders = new Dictionary<string, CardSlotRenderResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var (slotRef, slot) in slots)
        {
            if (string.Equals(slotRef, primarySlotRef, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // ADR-0113 switch slot: the overlay's TargetPayload picks the active payload; before
            // the first click a drill switch slot renders its awaiting hint (no query executes).
            var effectiveSlot = slot;
            var switchSlotAwaiting = false;
            if (slot.Payloads.Count > 0)
            {
                var overlayProbe = cellDrill.Get(card.Id);
                var payloadName = overlayProbe?.TargetPayload;
                var active = string.IsNullOrWhiteSpace(payloadName)
                    ? null
                    : slot.Payloads.FirstOrDefault(p =>
                        string.Equals(p.Name, payloadName, StringComparison.OrdinalIgnoreCase));
                if (active is null)
                {
                    if (drillTargets.IsTarget(slotRef))
                    {
                        switchSlotAwaiting = true;
                        active = slot.Payloads[0];
                    }
                    else
                    {
                        active = slot.Payloads[0];
                    }
                }

                effectiveSlot = new CardDiagramSlot
                {
                    SlotRef = slot.SlotRef,
                    Diagram = active.Diagram,
                    DataSource = slot.DataSource,
                    BoundFilters = slot.BoundFilters,
                    Legend = slot.Legend,
                    Presentation = slot.Presentation,
                    SeriesTransform = slot.SeriesTransform,
                    FlowInput = active.FlowInput ?? slot.FlowInput,
                    Payloads = slot.Payloads,
                };
            }

            var slotCard = card
                .WithDiagram(effectiveSlot.Diagram)
                .WithDataSource(effectiveSlot.DataSource)
                .WithBoundFilters(effectiveSlot.BoundFilters)
                .WithLegend(effectiveSlot.Legend)
                // A slot without its own presentation inherits the card's — the diagram modules'
                // chrome presets (e.g. table_drill_compact: width/density/column_filters) ride it.
                .WithPresentation(effectiveSlot.Presentation ?? card.Presentation)
                .WithSeriesTransform(effectiveSlot.SeriesTransform)
                .WithFlowInput(effectiveSlot.FlowInput ?? card.FlowInput);

            var errorContext = slotCard;
            try
            {
                // ADR-0107: a targeted overlay applies only to its slot; untargeted = legacy all slots.
                var drillOverlay = cellDrill.Get(card.Id);
                var slotOverlay =
                    drillOverlay is not null &&
                    !string.IsNullOrEmpty(drillOverlay.TargetSlot) &&
                    !string.Equals(drillOverlay.TargetSlot, slotRef, StringComparison.OrdinalIgnoreCase)
                        ? null
                        : drillOverlay;

                // Drill UX: a drill-target slot renders empty (with a hint) until its own click
                // fills it; the title template fills only when the overlay carries the values.
                var legacyAwaiting = drillTargets.IsTarget(slotRef) && slotOverlay is null;
                if (switchSlotAwaiting || legacyAwaiting)
                {
                    var awaitingKind = DiagramKindRegistry.Resolve(effectiveSlot.Diagram.Kind);
                    var awaitingSources = drillTargets.SourcesOf(slotRef);
                    renders[slotRef] = new CardSlotRenderResult(
                        slotRef,
                        effectiveSlot.Diagram.Kind,
                        awaitingKind.DataFamily,
                        string.Empty,
                        SlotTitle: DrillSlotTitleResolver.Resolve(
                            effectiveSlot.Diagram.Properties.GetValueOrDefault("title"),
                            null),
                        AwaitingClick: true,
                        // Several sources fill one switch slot — the generic hint covers them all.
                        AwaitingSource: awaitingSources.Count == 1 ? awaitingSources[0] : "switch");
                    continue;
                }

                var resolved = CardResolver.Resolve(slotCard, library, document.DashboardFilters);
                var effective = DocumentFlowBinder.MaterializeCard(resolved.Card, document);
                errorContext = effective;
                var query = QueryCompiler.Compile(
                    effective,
                    filters,
                    filterIndex,
                    document,
                    document.SqlDialect,
                    specDirectory,
                    slotOverlay,
                    ResolveReportTime(document));
                var rows = await connector.QueryAsync(query, cancellationToken).ConfigureAwait(false);
                rows = DashflowCardDataPipeline.ApplyTransforms(document, effective.FlowInput?.Value, rows);
                var kind = DiagramKindRegistry.Resolve(effective.Diagram.Kind);
                var renderPluginId = vizPlugins.Resolve(kind.DataFamily, kind.Id);
                var matrixPresentation = kind.DataFamily is DiagramDataFamily.Matrix
                    ? MatrixPresentation.FromCard(effective, library)
                    : null;
                var tablePresentation = kind.DataFamily is DiagramDataFamily.Table
                    ? CompositionResolution.ResolveTablePresentation(effective, library)
                    : null;
                var seriesTransform = kind.DataFamily is DiagramDataFamily.Chart or DiagramDataFamily.Matrix
                    ? CompositionResolution.ResolveSeriesTransform(effective, library)
                    : null;

                var slotResult = kind.DataFamily switch
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
                        Matrix: ChartDataBuilder.BuildHeatmap(rows, effective.Diagram, seriesTransform, effective.Tooltip?.Value),
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
                            document.ColorPalette?.Value)),
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
                renders[slotRef] = slotResult with
                {
                    SlotTitle = DrillSlotTitleResolver.Resolve(
                        effective.Diagram.Properties.GetValueOrDefault("title"),
                        slotOverlay),
                    FormatDefaults = document.ResolvedFormatDefaults,
                    // Клик-значения слота живут в display-кадре родительской оси (to_zone):
                    // для фильтров по storage-колонкам они конвертируются инверсией графа.
                    FlowPath = flowPath,
                };
            }
            catch (Exception ex)
            {
                renders[slotRef] = new CardSlotRenderResult(
                    slotRef,
                    slot.Diagram.Kind,
                    DiagramDataFamily.Table,
                    vizPlugins.Resolve(DiagramDataFamily.Table, slot.Diagram.Kind),
                    Error: RenderErrorFormatter.ForCard(ex, errorContext, slotRef),
                    FormatDefaults: document.ResolvedFormatDefaults);
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
            DashSpec.Abstractions.Data.DashDisplayDateTime display => LabelFormat.FormatObject(
                display,
                display.Civil.TimeOfDay == TimeSpan.Zero
                    ? LabelFormat.ResolveDateFormat(null)
                    : LabelFormat.ResolveDateTimeFormat(null)),
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
        var culture = LabelFormat.ResolveReportCulture();
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
            document.TimePolicy?.Value,
            viewerContext.ReportTimeSettings,
            LabelFormat.DisplayTimeZone?.Id);
}
