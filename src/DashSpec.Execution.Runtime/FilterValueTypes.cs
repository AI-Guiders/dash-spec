namespace DashSpec.Execution.Runtime;

public readonly record struct DateRangeValue(DateOnly From, DateOnly To);

/// <summary>Field selection: <see cref="Values"/> are active (ride the compiled query);
/// <see cref="InertValues"/> are stored-but-not-applied (ADR-0120 inert selection /
/// ADR-0062): the value left the current domain, stays visible grey with
/// <see cref="InertReason"/> as the tooltip, and one click removes it. When the domain
/// returns, the value becomes clickable again (moved back to active).</summary>
public readonly record struct FieldFilterValue(
    IReadOnlyList<string> Values,
    IReadOnlyList<string>? InertValues = null,
    string? InertReason = null)
{
    public bool HasSelection => Values.Count > 0;

    public bool HasInert => InertValues is { Count: > 0 };
}
