using DashSpec.Execution.Runtime;

namespace DashSpec.Execution.Runtime;

public sealed class FilterState
{
    private readonly Dictionary<string, DateRangeValue> _dates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FieldFilterValue> _fields = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _topLimits = new(StringComparer.OrdinalIgnoreCase);

    public void SetDate(string name, DateOnly from, DateOnly to) =>
        _dates[name] = new DateRangeValue(from, to);

    public void SetField(string name, IReadOnlyList<string> values) =>
        _fields[name] = new FieldFilterValue(values);

    /// <summary>ADR-0120 inert selection: revalidate a stored selection against the
    /// current option domain. Active values outside the domain move to inert (kept
    /// grey with <paramref name="reason"/>, excluded from the compiled query);
    /// inert values back inside the domain return to active. Nothing is silently
    /// dropped and nothing silently applies.</summary>
    public void ApplyDomain(string name, IReadOnlyList<string> domain, string reason)
    {
        if (!_fields.TryGetValue(name, out var current))
        {
            return;
        }

        var domainSet = new HashSet<string>(domain, StringComparer.OrdinalIgnoreCase);

        var active = current.Values.Where(v => domainSet.Contains(v)).ToList();
        var inert = current.Values.Where(v => !domainSet.Contains(v)).ToList();

        if (current.InertValues is { Count: > 0 })
        {
            inert.AddRange(current.InertValues.Where(v => !domainSet.Contains(v)));
            active.AddRange(current.InertValues.Where(v => domainSet.Contains(v)));
        }

        // order-stable dedup (a value may appear in both stores after a compose)
        active = active.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        inert = inert.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        _fields[name] = new FieldFilterValue(
            active,
            inert.Count > 0 ? inert : null,
            inert.Count > 0 ? reason : null);
    }

    /// <summary>One click: drop the inert residue entirely.</summary>
    public void ClearInert(string name)
    {
        if (!_fields.TryGetValue(name, out var current) || !current.HasInert)
        {
            return;
        }

        _fields[name] = new FieldFilterValue(current.Values);
    }

    /// <summary>Inert residue for the widget (grey entries + tooltip reason).</summary>
    public FieldFilterValue? GetFieldWithInert(string name) =>
        _fields.TryGetValue(name, out var value) ? value : null;

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
            // full record: the inert residue survives cloning (stored-but-not-applied)
            copy._fields[name] = field;
        }

        foreach (var (name, limit) in _topLimits)
        {
            copy.SetTop(name, limit);
        }

        return copy;
    }
}
