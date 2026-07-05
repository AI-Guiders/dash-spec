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
}
