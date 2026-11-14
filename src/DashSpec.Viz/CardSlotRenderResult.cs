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
    string? SlotTitle = null,
    bool AwaitingClick = false,
    string? AwaitingSource = null,
    string? Error = null,
    ReportFormatDefaults? FormatDefaults = null,
    DashflowExecutionPath? FlowPath = null,
    ChartPresentation? ChartPresentation = null,
    ChartPayload? DetailChart = null,
    MatrixPayload? DetailMatrix = null,
    string? Number = null,
    string? NumberScale = null,
    string? NumberDelta = null,
    string? NumberDeltaTone = null,
    string? EmptyMessage = null,
    DashSpec.Modeling.Parse.Card.CardClickBehaviour? ClickBehaviour = null,
    IReadOnlyList<DashSpec.Modeling.Parse.Card.CardClickHandler>? ClickHandlers = null);
