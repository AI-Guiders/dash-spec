using DashSpec.Core.Model;
using Microsoft.AspNetCore.Components;

namespace DashSpec.Filters;

/// <summary>Host → filter widget plugin contract (ADR-0060).</summary>
public sealed class FilterWidgetRenderContext
{
    public required FilterDefinition Filter { get; init; }

    public string Label { get; init; } = string.Empty;

    public string? Hint { get; init; }

    public bool Compact { get; init; }

    public bool Disabled { get; init; }

    public string? PlacementStyle { get; init; }

    public bool ShowDateLabel { get; init; }

    public string DateLabelClass { get; init; } = "filter-label";

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
