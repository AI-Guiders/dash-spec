using DashSpec.Core.Model;
using DashSpec.Core.Runtime;
using DashSpec.Host.Services.Abstractions;

namespace DashSpec.Host.Services.Presentation;

/// <summary>UI-side mirror of dashboard filter values (bound to Blazor widgets).</summary>
public sealed class DashboardFilterUiState
{
    public Dictionary<string, DateOnly> DateFrom { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, DateOnly> DateTo { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, HashSet<string>> SelectedFields { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, int> TopLimits { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void Clear()
    {
        DateFrom.Clear();
        DateTo.Clear();
        SelectedFields.Clear();
        TopLimits.Clear();
    }

    public void LoadFromSession(IDashboardSession session, IEnumerable<string> placedFilterNames)
    {
        Clear();
        foreach (var filterName in placedFilterNames)
        {
            if (!session.FilterIndex.TryGetValue(filterName, out var filter))
            {
                continue;
            }

            if (filter.Kind is FilterKind.Date)
            {
                var range = session.Filters.GetDate(filter.Name)
                    ?? DateDefaultRange.Resolve(filter.DefaultExpression!, DateOnly.FromDateTime(DateTime.UtcNow));
                DateFrom[filter.Name] = range.From;
                DateTo[filter.Name] = range.To;
            }
            else if (filter.Kind is FilterKind.Field)
            {
                var values = session.Filters.GetField(filter.Name)?.Values
                    ?? FieldFilterDefaults.ResolveValues(filter.DefaultExpression);
                SelectedFields[filter.Name] = values.ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            else if (filter.Kind is FilterKind.Top)
            {
                TopLimits[filter.Name] = TopLimitDefaults.Resolve(
                    filter,
                    session.Filters.GetTop(filter.Name));
            }
        }
    }

    public void SyncToSession(IDashboardSession session, IEnumerable<string> placedFilterNames)
    {
        foreach (var filterName in placedFilterNames)
        {
            if (!session.FilterIndex.TryGetValue(filterName, out var filter))
            {
                continue;
            }

            if (filter.Kind is FilterKind.Date &&
                DateFrom.TryGetValue(filter.Name, out var from) &&
                DateTo.TryGetValue(filter.Name, out var to))
            {
                session.ApplyDateFilter(filter.Name, from, to);
            }
            else if (filter.Kind is FilterKind.Field &&
                     SelectedFields.TryGetValue(filter.Name, out var selected))
            {
                session.ApplyFieldFilter(filter.Name, selected);
            }
            else if (filter.Kind is FilterKind.Top &&
                     TopLimits.TryGetValue(filter.Name, out var topLimit))
            {
                session.ApplyTopFilter(filter.Name, TopLimitDefaults.Resolve(filter, topLimit));
            }
        }
    }

    public FilterUiSnapshot Capture() =>
        new(
            SelectedFields.ToDictionary(
                x => x.Key,
                x => new HashSet<string>(x.Value, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, DateOnly>(DateFrom, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, DateOnly>(DateTo, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(TopLimits, StringComparer.OrdinalIgnoreCase));

    public void ApplySnapshot(
        FilterUiSnapshot snapshot,
        IReadOnlyDictionary<string, FilterDefinition> filterIndex)
    {
        foreach (var (name, values) in snapshot.SelectedFields)
        {
            if (!filterIndex.ContainsKey(name))
            {
                continue;
            }

            SelectedFields[name] = new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
        }

        foreach (var (name, from) in snapshot.DateFrom)
        {
            if (!filterIndex.ContainsKey(name))
            {
                continue;
            }

            DateFrom[name] = from;
        }

        foreach (var (name, to) in snapshot.DateTo)
        {
            if (!filterIndex.ContainsKey(name))
            {
                continue;
            }

            DateTo[name] = to;
        }

        foreach (var (name, limit) in snapshot.TopLimits)
        {
            if (!filterIndex.ContainsKey(name))
            {
                continue;
            }

            TopLimits[name] = limit;
        }
    }
}

public sealed record FilterUiSnapshot(
    IReadOnlyDictionary<string, HashSet<string>> SelectedFields,
    IReadOnlyDictionary<string, DateOnly> DateFrom,
    IReadOnlyDictionary<string, DateOnly> DateTo,
    IReadOnlyDictionary<string, int> TopLimits)
{
    public FilterUiSnapshot NarrowTo(IEnumerable<string> filterNames)
    {
        var names = filterNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new FilterUiSnapshot(
            SelectedFields
                .Where(x => names.Contains(x.Key))
                .ToDictionary(
                    x => x.Key,
                    x => new HashSet<string>(x.Value, StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase),
            DateFrom
                .Where(x => names.Contains(x.Key))
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
            DateTo
                .Where(x => names.Contains(x.Key))
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
            TopLimits
                .Where(x => names.Contains(x.Key))
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase));
    }
}
