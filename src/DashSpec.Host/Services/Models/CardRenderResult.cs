using DashSpec.Core.Model;
using DashSpec.Core.Runtime;

namespace DashSpec.Host.Services.Models;

public sealed record CardRenderResult(
    string Id,
    string Title,
    string DiagramKind,
    DiagramDataFamily DataFamily,
    string RenderPluginId,
    ChartPayload? Chart = null,
    TablePayload? Table = null,
    string? Number = null,
    string? Error = null,
    bool Loading = false,
    IReadOnlyList<string>? BoundFilters = null,
    IReadOnlyList<string>? LocalFilters = null,
    PlacementDefinition? Placement = null,
    ChartPresentation? ChartPresentation = null,
    MatrixPayload? Matrix = null,
    MatrixPresentation? MatrixPresentation = null);

public sealed class DashboardHostOptions
{
    public const string SectionName = "Dashboard";

    public string SpecPath { get; set; } = string.Empty;
}
