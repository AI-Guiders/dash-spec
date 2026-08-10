using System.Globalization;
using DashSpec.Abstractions.Connectors;
using DashSpec.Core.Compilation;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;
using DashSpec.Host.Plugins;
using DashSpec.Host.Services.Abstractions;
using DashSpec.Host.Services.Models;
using DashSpec.Host.Services.Presentation;

namespace DashSpec.Host.Services.Rendering;

public sealed class CardRenderService(VizPluginRegistry vizPlugins) : ICardRenderer
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

        var resolved = CardResolver.Resolve(card, library, document.DashboardFilters);
        var effective = resolved.Card;
        var query = QueryCompiler.Compile(effective, filters, filterIndex, document.SqlDialect, specDirectory);
        var rows = await connector.QueryAsync(query, cancellationToken).ConfigureAwait(false);
        var kind = DiagramKindRegistry.Resolve(effective.Diagram.Kind);
        var chartPresentation = kind.DataFamily is DiagramDataFamily.Chart
            ? CardChromeResolver.ResolveChartPresentation(effective, library)
            : null;
        var seriesTransform = kind.DataFamily is DiagramDataFamily.Chart or DiagramDataFamily.Matrix
            ? CardChromeResolver.ResolveSeriesTransform(effective, library)
            : null;
        var matrixPresentation = kind.DataFamily is DiagramDataFamily.Matrix
            ? MatrixPresentation.FromCard(effective, library)
            : null;
        var renderPluginId = vizPlugins.Resolve(resolved.RenderPluginId, kind.DataFamily);
        var interiorPlacements = card.InteriorBoard is null
            ? null
            : DashboardLayoutHelper.ResolveInteriorPlacements(card, document);

        var (filterLinkHint, filterLinkCssClass) = CardFilterLinkHints.Resolve(card, document);
        var topFilterScopeHint = CardFilterScopeHints.ResolveTopFilterScope(card, document);

        return kind.DataFamily switch
        {
            DiagramDataFamily.Chart =>
                new CardRenderResult(
                    card.Id,
                    card.Title,
                    effective.Diagram.Kind,
                    kind.DataFamily,
                    renderPluginId,
                    Chart: ChartDataBuilder.BuildLineOrBar(rows, effective.Diagram, seriesTransform, effective, library, document.ColorPalette),
                    Placement: card.Placement,
                    InteriorPlacements: interiorPlacements,
                    ChartPresentation: chartPresentation,
                    BoundFilters: card.BoundFilters,
                    LocalFilters: card.LocalFilters,
                    ClickBehaviour: card.ClickBehaviour,
                    ExtensionBlocks: card.ExtensionBlocks,
                    LocalFiltersManualApply: card.LocalFiltersManualApply,
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
                    BoundFilters: card.BoundFilters,
                    LocalFilters: card.LocalFilters,
                    ClickBehaviour: card.ClickBehaviour,
                    ExtensionBlocks: card.ExtensionBlocks,
                    LocalFiltersManualApply: card.LocalFiltersManualApply,
                    MatrixLimits: card.MatrixLimits,
                    OversizeMessage: card.OversizeMessage,
                    FilterLinkHint: filterLinkHint,
                    FilterLinkCssClass: filterLinkCssClass,
                    TopFilterScopeHint: topFilterScopeHint),
            DiagramDataFamily.Scalar =>
                new CardRenderResult(
                    card.Id,
                    card.Title,
                    effective.Diagram.Kind,
                    kind.DataFamily,
                    renderPluginId,
                    Number: FormatNumber(rows, effective.Diagram),
                    Placement: card.Placement,
                    InteriorPlacements: interiorPlacements,
                    BoundFilters: card.BoundFilters,
                    LocalFilters: card.LocalFilters,
                    ClickBehaviour: card.ClickBehaviour,
                    ExtensionBlocks: card.ExtensionBlocks,
                    LocalFiltersManualApply: card.LocalFiltersManualApply,
                    MatrixLimits: card.MatrixLimits,
                    OversizeMessage: card.OversizeMessage),
            DiagramDataFamily.Matrix =>
                new CardRenderResult(
                    card.Id,
                    card.Title,
                    effective.Diagram.Kind,
                    kind.DataFamily,
                    renderPluginId,
                    Matrix: ChartDataBuilder.BuildHeatmap(rows, effective.Diagram, seriesTransform),
                    Placement: card.Placement,
                    InteriorPlacements: interiorPlacements,
                    MatrixPresentation: matrixPresentation,
                    BoundFilters: card.BoundFilters,
                    LocalFilters: card.LocalFilters,
                    ClickBehaviour: card.ClickBehaviour,
                    ExtensionBlocks: card.ExtensionBlocks,
                    LocalFiltersManualApply: card.LocalFiltersManualApply,
                    MatrixLimits: card.MatrixLimits,
                    OversizeMessage: card.OversizeMessage,
                    FilterLinkHint: filterLinkHint,
                    FilterLinkCssClass: filterLinkCssClass,
                    TopFilterScopeHint: topFilterScopeHint),
            _ => throw new ArgumentOutOfRangeException(nameof(card)),
        };
    }

    private static string? FormatNumber(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        DiagramDefinition diagram)
    {
        if (rows.Count == 0)
        {
            return null;
        }

        var value = rows[0].GetValueOrDefault(DiagramBindings.Column(diagram, "value"));
        if (value is null or DBNull)
        {
            return null;
        }

        return value switch
        {
            DateOnly d => d.ToString("dd.MM.yyyy"),
            DateTime dt => dt.TimeOfDay == TimeSpan.Zero
                ? dt.ToString("dd.MM.yyyy")
                : dt.ToString("dd.MM.yyyy HH:mm"),
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
}
