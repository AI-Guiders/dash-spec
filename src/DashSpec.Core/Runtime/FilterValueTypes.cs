namespace DashSpec.Core.Runtime;

public readonly record struct DateRangeValue(DateOnly From, DateOnly To);

public readonly record struct FieldFilterValue(IReadOnlyList<string> Values)
{
    public bool HasSelection => Values.Count > 0;
}
