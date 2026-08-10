namespace DashSpec.Core.Runtime;

public sealed class FilterState
{
    private readonly Dictionary<string, DateRangeValue> _dates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FieldFilterValue> _fields = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _topLimits = new(StringComparer.OrdinalIgnoreCase);

    public void SetDate(string name, DateOnly from, DateOnly to) =>
        _dates[name] = new DateRangeValue(from, to);

    public void SetField(string name, IReadOnlyList<string> values) =>
        _fields[name] = new FieldFilterValue(values);

    public void SetTop(string name, int limit) =>
        _topLimits[name] = limit;

    public DateRangeValue? GetDate(string name) =>
        _dates.TryGetValue(name, out var value) ? value : null;

    public FieldFilterValue? GetField(string name) =>
        _fields.TryGetValue(name, out var value) ? value : null;

    public int? GetTop(string name) =>
        _topLimits.TryGetValue(name, out var value) ? value : null;

    public IReadOnlyDictionary<string, DateRangeValue> Dates => _dates;
    public IReadOnlyDictionary<string, FieldFilterValue> Fields => _fields;
    public IReadOnlyDictionary<string, int> TopLimits => _topLimits;

    public FilterState Clone()
    {
        var copy = new FilterState();
        foreach (var (name, range) in _dates)
        {
            copy.SetDate(name, range.From, range.To);
        }

        foreach (var (name, field) in _fields)
        {
            copy.SetField(name, field.Values.ToList());
        }

        foreach (var (name, limit) in _topLimits)
        {
            copy.SetTop(name, limit);
        }

        return copy;
    }
}

public readonly record struct DateRangeValue(DateOnly From, DateOnly To);

public readonly record struct FieldFilterValue(IReadOnlyList<string> Values)
{
    public bool HasSelection => Values.Count > 0;
}
