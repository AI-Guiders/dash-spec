using DashSpec.Abstractions.Connectors;
using DashSpec.Core.Compilation;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;
using DashSpec.Host.Plugins;
using DashSpec.Host.Services.Abstractions;
using DashSpec.Host.Services.Models;

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
        var seriesTransform = kind.DataFamily is DiagramDataFamily.Chart
            ? CardChromeResolver.ResolveSeriesTransform(effective, library)
            : null;
        var matrixPresentation = kind.DataFamily is DiagramDataFamily.Matrix
            ? MatrixPresentation.FromCard(effective, library)
            : null;
        var renderPluginId = vizPlugins.Resolve(resolved.RenderPluginId, kind.DataFamily);

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
                    ChartPresentation: chartPresentation,
                    BoundFilters: card.BoundFilters,
                    LocalFilters: card.LocalFilters),
            DiagramDataFamily.Table =>
                new CardRenderResult(
                    card.Id,
                    card.Title,
                    effective.Diagram.Kind,
                    kind.DataFamily,
                    renderPluginId,
                    Table: ChartDataBuilder.BuildTable(rows, effective.Diagram),
                    Placement: card.Placement,
                    BoundFilters: card.BoundFilters,
                    LocalFilters: card.LocalFilters),
            DiagramDataFamily.Scalar =>
                new CardRenderResult(
                    card.Id,
                    card.Title,
                    effective.Diagram.Kind,
                    kind.DataFamily,
                    renderPluginId,
                    Number: FormatNumber(rows, effective.Diagram),
                    Placement: card.Placement,
                    BoundFilters: card.BoundFilters,
                    LocalFilters: card.LocalFilters),
            DiagramDataFamily.Matrix =>
                new CardRenderResult(
                    card.Id,
                    card.Title,
                    effective.Diagram.Kind,
                    kind.DataFamily,
                    renderPluginId,
                    Matrix: ChartDataBuilder.BuildHeatmap(rows, effective.Diagram),
                    Placement: card.Placement,
                    MatrixPresentation: matrixPresentation,
                    BoundFilters: card.BoundFilters,
                    LocalFilters: card.LocalFilters),
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

        return Convert.ToString(rows[0].GetValueOrDefault(DiagramBindings.Column(diagram, "value")));
    }
}
