namespace DashSpec.Core.Runtime;

/// <summary>Per-card heatmap cell selection applied only to interior drill SQL (ADR-0064).</summary>
public sealed class CardCellDrillOverlay
{
    private readonly Dictionary<string, DateRangeValue> _dates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _fields = new(StringComparer.OrdinalIgnoreCase);

    public void SetDate(string filterName, DateOnly from, DateOnly to) =>
        _dates[filterName] = new DateRangeValue(from, to);

    public void SetField(string filterName, string value) =>
        _fields[filterName] = new HashSet<string>([value], StringComparer.OrdinalIgnoreCase);

    public bool TryGetDate(string filterName, out DateRangeValue range) =>
        _dates.TryGetValue(filterName, out range);

    public bool TryGetField(string filterName, out IReadOnlySet<string> values)
    {
        if (_fields.TryGetValue(filterName, out var set))
        {
            values = set;
            return true;
        }

        values = null!;
        return false;
    }

    public bool IsEmpty => _dates.Count == 0 && _fields.Count == 0;
}
