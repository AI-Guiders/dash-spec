using DashSpec.Core.Model;
using DashSpec.Core.Runtime;
using DashSpec.Execution.Runtime;
using DashSpec.Abstractions.Hosting;
using DashSpec.Host.Services.Abstractions;

namespace DashSpec.Host.Services.Presentation;

/// <summary>Per-card UI values for filters placed on <c>card filters { }</c> (not dashboard toolbar).</summary>
public sealed class CardLocalFilterUiStore
{
    private readonly Dictionary<string, Dictionary<string, DateOnly>> _dateFrom = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, DateOnly>> _dateTo = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, HashSet<string>>> _fields = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, int>> _topLimits = new(StringComparer.OrdinalIgnoreCase);

    public void Clear()
    {
        _dateFrom.Clear();
        _dateTo.Clear();
        _fields.Clear();
        _topLimits.Clear();
    }

    public void SeedFromDocument(IDashboardSession session)
    {
        Clear();
        var index = session.FilterIndex;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var card in session.Document.Cards)
        {
            if (card.LocalFilters is not { Count: > 0 })
            {
                continue;
            }

            EnsureCard(card.Id);
            foreach (var filterName in card.LocalFilters)
            {
                if (!index.TryGetValue(filterName, out var filter))
                {
                    continue;
                }

                SeedFilter(card.Id, filter, today);
            }
        }
    }

    public void SetDate(string cardId, string filterName, DateOnly from, DateOnly to)
    {
        EnsureCard(cardId);
        _dateFrom[cardId][filterName] = from;
        _dateTo[cardId][filterName] = to;
    }

    public void SetField(string cardId, string filterName, HashSet<string> values)
    {
        EnsureCard(cardId);
        _fields[cardId][filterName] = values;
    }

    public void SetTop(string cardId, string filterName, int limit)
    {
        EnsureCard(cardId);
        _topLimits[cardId][filterName] = limit;
    }

    public Dictionary<string, DateOnly> DateFromBinding(string cardId)
    {
        EnsureCard(cardId);
        return _dateFrom[cardId];
    }

    public Dictionary<string, DateOnly> DateToBinding(string cardId)
    {
        EnsureCard(cardId);
        return _dateTo[cardId];
    }

    public Dictionary<string, HashSet<string>> SelectedFieldsBinding(string cardId)
    {
        EnsureCard(cardId);
        return _fields[cardId];
    }

    public Dictionary<string, int> TopLimitsBinding(string cardId)
    {
        EnsureCard(cardId);
        return _topLimits[cardId];
    }

    public FilterState ComposeQueryFilters(
        FilterState sessionFilters,
        CardDefinition card,
        IReadOnlyDictionary<string, FilterDefinition> filterIndex) =>
        CardQueryFilterState.Compose(
            sessionFilters,
            card,
            filterIndex,
            BuildDateRanges(card.Id),
            BuildFieldValues(card.Id),
            BuildTopLimits(card.Id));

    private IReadOnlyDictionary<string, DateRangeValue>? BuildDateRanges(string cardId)
    {
        if (!_dateFrom.TryGetValue(cardId, out var fromMap) || !_dateTo.TryGetValue(cardId, out var toMap))
        {
            return null;
        }

        var result = new Dictionary<string, DateRangeValue>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, from) in fromMap)
        {
            if (toMap.TryGetValue(name, out var to))
            {
                result[name] = new DateRangeValue(from, to);
            }
        }

        return result;
    }

    private IReadOnlyDictionary<string, FieldFilterValue>? BuildFieldValues(string cardId)
    {
        if (!_fields.TryGetValue(cardId, out var map))
        {
            return null;
        }

        return map.ToDictionary(
            x => x.Key,
            x => new FieldFilterValue(x.Value.ToList()),
            StringComparer.OrdinalIgnoreCase);
    }

    private IReadOnlyDictionary<string, int>? BuildTopLimits(string cardId) =>
        _topLimits.TryGetValue(cardId, out var map) ? map : null;

    private bool TryGetDate(string cardId, string filterName, out DateOnly from, out DateOnly to)
    {
        from = default;
        to = default;
        return _dateFrom.TryGetValue(cardId, out var fromMap) &&
               _dateTo.TryGetValue(cardId, out var toMap) &&
               fromMap.TryGetValue(filterName, out from) &&
               toMap.TryGetValue(filterName, out to);
    }

    private bool TryGetField(string cardId, string filterName, out HashSet<string> values)
    {
        values = [];
        return _fields.TryGetValue(cardId, out var map) &&
               map.TryGetValue(filterName, out values!);
    }

    private bool TryGetTop(string cardId, string filterName, out int limit)
    {
        limit = 0;
        return _topLimits.TryGetValue(cardId, out var map) && map.TryGetValue(filterName, out limit);
    }

    private void EnsureCard(string cardId)
    {
        if (!_dateFrom.ContainsKey(cardId))
        {
            _dateFrom[cardId] = new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase);
            _dateTo[cardId] = new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase);
            _fields[cardId] = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            _topLimits[cardId] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SeedFilter(string cardId, FilterDefinition filter, DateOnly today)
    {
        switch (filter.Kind)
        {
            case FilterKind.Date:
                var range = DateDefaultRange.Resolve(filter.DefaultExpression!, today);
                SetDate(cardId, filter.Name, range.From, range.To);
                break;
            case FilterKind.Field:
                var values = FieldFilterDefaults.ResolveValues(filter.DefaultExpression);
                SetField(cardId, filter.Name, values.ToHashSet(StringComparer.OrdinalIgnoreCase));
                break;
            case FilterKind.Top:
                SetTop(cardId, filter.Name, TopLimitDefaults.Resolve(filter, null));
                break;
        }
    }
}
