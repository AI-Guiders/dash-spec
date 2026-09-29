using DashSpec.Core.Model;
using DashSpec.Core.Runtime;
using DashSpec.Execution.Runtime;

namespace DashSpec.Viz;

public sealed record CardSlotRenderResult(
    string SlotRef,
    string DiagramKind,
    DiagramDataFamily DataFamily,
    string RenderPluginId,
    ChartPayload? Chart = null,
    TablePayload? Table = null,
    MatrixPayload? Matrix = null,
    GanttPayload? Gantt = null,
    MatrixPresentation? MatrixPresentation = null,
    TablePresentation? TablePresentation = null,
    string? Error = null);
