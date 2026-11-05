using DashSpec.Core.Interaction;
using DashSpec.Execution.Runtime;
using Microsoft.AspNetCore.Components;

namespace DashSpec.Viz;

public sealed class CardVizRenderContext
{
    public required CardRenderResult Card { get; init; }

    /// <summary>Session overrides (legend, axes) are keyed by host card id, not interior slot synthetic ids.</summary>
    public string? VizStateCardId { get; init; }

    public bool DetailView { get; init; }

    public double MatrixMin { get; init; }

    public double MatrixMax { get; init; }

    public EventCallback<HeatmapCellContext> OnHeatmapCellSelected { get; init; }

    /// <summary>Category-item (axis label) clicks — ADR-0107; the context carries ClickSource.</summary>
    public EventCallback<HeatmapCellContext> OnHeatmapCategoryItemSelected { get; init; }

    /// <summary>Граф (G) получения данных: клик-значения фильтров конвертируются через G⁻¹.</summary>
    public DashflowExecutionPath? FlowPath { get; init; }

    public CardRenderResult EffectiveCard => Card.ForView(DetailView);

    public string ResolveVizStateCardId() =>
        string.IsNullOrWhiteSpace(VizStateCardId) ? Card.Id : VizStateCardId;
}
