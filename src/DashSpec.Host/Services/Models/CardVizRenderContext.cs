using DashSpec.Core.Model;
using Microsoft.AspNetCore.Components;

namespace DashSpec.Host.Services.Models;

public sealed class CardVizRenderContext
{
    public required CardRenderResult Card { get; init; }

    public double MatrixMin { get; init; }

    public double MatrixMax { get; init; }

    public EventCallback<HeatmapCellContext> OnHeatmapCellSelected { get; init; }
}
