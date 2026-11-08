using DashSpec.Abstractions.Data;
using DashSpec.Execution.Runtime;
using Microsoft.AspNetCore.Components;

namespace DashSpec.Plugin.Viz.RadzenUI.Components;

/// <summary>
/// ADR-0124: the typed table grid. The conveyor materializes CLR rows (dsc build types)
/// and hands them over in the payload — this component only displays them. The grid's
/// native machinery works on real properties: sorting, filtering, and Radzen's own
/// grouping (drag the ⋮ handle, collapsible group rows). No type resolution here.
/// </summary>
public sealed partial class TypedTableGrid<TItem> where TItem : notnull
{
    [Parameter] public IReadOnlyList<TableField> Fields { get; set; } = [];

    [Parameter] public IReadOnlyList<TItem> Items { get; set; } = [];

    [Parameter] public bool ColumnFilters { get; set; } = true;

    [Parameter] public int FrozenColumns { get; set; } = 0;
}
