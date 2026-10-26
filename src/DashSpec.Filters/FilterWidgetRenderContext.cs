using Microsoft.AspNetCore.Components;
using DashSpec.Modeling.Parse.Filter;

namespace DashSpec.Filters;

/// <summary>Host → filter widget plugin contract (ADR-0060).</summary>
public sealed class FilterWidgetRenderContext
{
    public required FilterDefinition Filter { get; init; }

    public string Label { get; init; } = string.Empty;

    public string? Hint { get; init; }

    public string? HintAriaLabel { get; init; }

    public bool Compact { get; init; }

    /// <summary>Toolbar chrome <c>layout = bar</c> with labeled cells (spec <c>cells</c>, default labeled).</summary>
    public bool ToolbarLabeledCells { get; init; }

    public bool Disabled { get; init; }

    public string? PlacementStyle { get; init; }

    /// <summary>Visual label switch: false in the labeled toolbar (the section owns the
    /// label row); controls keep their own aria-labels either way.</summary>
    public bool ShowLabel { get; init; } = true;

    public bool ShowDateLabel { get; init; }

    public IReadOnlyList<string> Options { get; init; } = [];

    public HashSet<string> Selected { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public EventCallback<HashSet<string>> SelectedChanged { get; init; }

    public Dictionary<string, DateOnly> DateFrom { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, DateOnly> DateTo { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, FilterDefinition> FilterIndex { get; init; } =
        new Dictionary<string, FilterDefinition>(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, HashSet<string>> SelectedFields { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    public EventCallback<string> OnDayChanged { get; init; }

    public EventCallback OnRangeChanged { get; init; }

    public int TopValue { get; init; }

    public EventCallback<int> TopValueChanged { get; init; }
}
