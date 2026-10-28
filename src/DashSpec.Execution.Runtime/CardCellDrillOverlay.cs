namespace DashSpec.Execution.Runtime;

/// <summary>Per-card heatmap cell selection applied only to interior drill SQL (ADR-0064).</summary>
public sealed class CardCellDrillOverlay
{
    private readonly Dictionary<string, DateRangeValue> _dates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _fields = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Display-кадр (как на оси) для заголовков; query-значения живут в _fields.</summary>
    private readonly Dictionary<string, string> _fieldDisplay = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Target slot ref of the drill this overlay came from (ADR-0107): the overlay applies
    /// only to that interior slot. Empty = legacy apply-to-all-drill-slots behavior.
    /// </summary>
    public string TargetSlot { get; private set; } = string.Empty;

    public void SetTargetSlot(string slotRef) => TargetSlot = slotRef;

    /// <summary>Active payload of the target slot when the drill addresses a switch slot (ADR-0113).</summary>
    public string TargetPayload { get; private set; } = string.Empty;

    public void SetTargetPayload(string payloadName) => TargetPayload = payloadName;

    public void SetDate(string filterName, DateOnly from, DateOnly to) =>
        _dates[filterName] = new DateRangeValue(from, to);

    public void SetField(string filterName, string value) =>
        _fields[filterName] = new HashSet<string>([value], StringComparer.OrdinalIgnoreCase);

    /// <summary>Значение в display-кадре (метка оси) для заголовков; query читает SetField.</summary>
    public void SetFieldDisplay(string filterName, string value) =>
        _fieldDisplay[filterName] = value;

    private readonly Dictionary<string, (DateTime From, DateTime To)> _dateTimeRanges = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Диапазон storage-времени для drill-фильтра по datetime-колонке (точный интервал ячейки).</summary>
    public void SetDateTimeRange(string filterName, DateTime from, DateTime to) =>
        _dateTimeRanges[filterName] = (from, to);

    public bool TryGetDateTimeRange(string filterName, out (DateTime From, DateTime To) range)
    {
        if (_dateTimeRanges.TryGetValue(filterName, out var value))
        {
            range = value;
            return true;
        }

        range = default;
        return false;
    }

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

    public bool TryGetFieldDisplay(string filterName, out string value) =>
        _fieldDisplay.TryGetValue(filterName, out value!);

    /// <summary>Filter names this overlay carries (ADR-0109: the click bind is the overlay's own scope).</summary>
    public IEnumerable<string> FilterNames
    {
        get
        {
            foreach (var name in _dates.Keys)
            {
                yield return name;
            }

            foreach (var name in _dateTimeRanges.Keys)
            {
                yield return name;
            }

            foreach (var name in _fields.Keys)
            {
                yield return name;
            }
        }
    }

    public bool IsEmpty => _dates.Count == 0 && _fields.Count == 0 && _dateTimeRanges.Count == 0;
}
