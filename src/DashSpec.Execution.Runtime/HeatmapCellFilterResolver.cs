using System.Globalization;
using DashSpec.Core.Model;
using DashSpec.Core.Runtime;

namespace DashSpec.Execution.Runtime;

/// <summary>Click → filter wire values for heatmap cell drill (ADR-0028 / ADR-0064).</summary>
public static class HeatmapCellFilterResolver
{
    public static CardCellDrillOverlay BuildOverlay(
        IEnumerable<SetFilterFromFieldEffect> binds,
        HeatmapCellContext context,
        IReadOnlyDictionary<string, FilterDefinition> filterIndex,
        FilterState filters,
        IEnumerable<string>? anchorDateFilterNames = null)
    {
        var overlay = new CardCellDrillOverlay();
        foreach (var bind in binds)
        {
            if (!filterIndex.TryGetValue(bind.FilterName, out var filter))
            {
                continue;
            }

            var raw = ReadRaw(bind.Field, context);
            if (!IsDrillableRaw(raw))
            {
                continue;
            }

            switch (filter.Kind)
            {
                case FilterKind.Date when TryParseHeatmapDateFilter(raw, bind.FilterName, filters, out var day):
                    overlay.SetDate(bind.FilterName, day, day);
                    break;
                case FilterKind.Field:
                    var wire = ResolveFieldWireValue(filter, raw, context, filters, anchorDateFilterNames);
                    if (!string.IsNullOrWhiteSpace(wire))
                    {
                        overlay.SetField(bind.FilterName, wire);
                    }

                    break;
            }
        }

        return overlay;
    }

    public static void ApplyToSessionUi(
        SetFilterFromFieldEffect effect,
        HeatmapCellContext context,
        IReadOnlyDictionary<string, FilterDefinition> filterIndex,
        FilterState filters,
        Action<string, DateOnly, DateOnly> setDate,
        Action<string, string> setField,
        IEnumerable<string>? anchorDateFilterNames = null)
    {
        if (!filterIndex.TryGetValue(effect.FilterName, out var filter))
        {
            return;
        }

        var raw = ReadRaw(effect.Field, context);
        if (!IsDrillableRaw(raw))
        {
            return;
        }

        switch (filter.Kind)
        {
            case FilterKind.Date when TryParseHeatmapDateFilter(raw, effect.FilterName, filters, out var day):
                setDate(effect.FilterName, day, day);
                break;
            case FilterKind.Field:
                var wire = ResolveFieldWireValue(filter, raw, context, filters, anchorDateFilterNames);
                if (!string.IsNullOrWhiteSpace(wire))
                {
                    setField(effect.FilterName, wire);
                }

                break;
        }
    }

    public static IEnumerable<SetFilterFromFieldEffect> CollectDrillBinds(IReadOnlyList<CardClickEffect> effects)
    {
        var drill = effects.OfType<DrillTableFromCellEffect>().FirstOrDefault();
        if (drill is null)
        {
            return effects.OfType<SetFilterFromFieldEffect>();
        }

        return drill.Binds
            .Concat(effects.OfType<SetFilterFromFieldEffect>())
            .GroupBy(static b => b.FilterName, StringComparer.OrdinalIgnoreCase)
            .Select(static g => g.First());
    }

    public static IEnumerable<string> ResolveAnchorDateFilterNames(
        IReadOnlyList<string>? localFilters,
        IReadOnlyList<string>? boundFilters,
        IReadOnlyDictionary<string, FilterDefinition> filterIndex)
    {
        var names = new List<string>();
        if (localFilters is not null)
        {
            names.AddRange(localFilters);
        }

        if (boundFilters is not null)
        {
            names.AddRange(boundFilters);
        }

        return names
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(name =>
                filterIndex.TryGetValue(name, out var filter) &&
                filter.Kind is FilterKind.Date);
    }

    private static string ReadRaw(string field, HeatmapCellContext context) =>
        field switch
        {
            "x" => context.XLabel,
            "y" => context.YLabel,
            "value" => context.Value?.ToString("0") ?? string.Empty,
            _ => string.Empty,
        };

    private static bool IsDrillableRaw(string raw) =>
        !string.IsNullOrWhiteSpace(raw) &&
        !string.Equals(raw, "Other", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(raw, "Прочие", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseHeatmapDateFilter(
        string raw,
        string filterName,
        FilterState filters,
        out DateOnly day)
    {
        if (DateValueCodec.TryParseWireDay(raw, out day))
        {
            return true;
        }

        var range = filters.GetDate(filterName);
        var anchorYear = range?.To.Year ?? range?.From.Year ?? DateTime.UtcNow.Year;
        return DateValueCodec.TryParseChartAxisDayLabel(raw, anchorYear, out day);
    }

    private static string? ResolveFieldWireValue(
        FilterDefinition filter,
        string raw,
        HeatmapCellContext context,
        FilterState filters,
        IEnumerable<string>? anchorDateFilterNames)
    {
        var column = filter.ColumnReference ?? filter.Name;

        if (column.Contains("bucket_start_utc", StringComparison.OrdinalIgnoreCase) &&
            context.XBucketUtc is { } bucketUtc)
        {
            var storageUtc = DateValueCodec.NormalizeStorageUtc(bucketUtc);
            return storageUtc.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        }

        if (!TryParseHeatmapTimeLabel(raw, out var time))
        {
            return raw;
        }

        if (column.Contains("bucket_start_utc", StringComparison.OrdinalIgnoreCase))
        {
            var anchor = ResolveAnchorDay(anchorDateFilterNames, filters);
            if (anchor is null)
            {
                return raw;
            }

            var storageBucket = LabelFormat.DisplayTimeZone is { } displayTz
                ? DateValueCodec.CombineDisplayLocalToStorageUtc(anchor.Value, time, displayTz)
                : DateValueCodec.NormalizeStorageUtc(anchor.Value.ToDateTime(time, DateTimeKind.Unspecified));
            return storageBucket.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        }

        if (column.Contains("bucket_hour_hhmm", StringComparison.OrdinalIgnoreCase))
        {
            var hourFloor = new TimeOnly(time.Hour, 0);
            return hourFloor.ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        return raw;
    }

    private static DateOnly? ResolveAnchorDay(IEnumerable<string>? anchorDateFilterNames, FilterState filters)
    {
        if (anchorDateFilterNames is not null)
        {
            foreach (var name in anchorDateFilterNames)
            {
                var range = filters.GetDate(name);
                if (range is not null)
                {
                    return range.Value.To;
                }
            }
        }

        return null;
    }

    private static bool TryParseHeatmapTimeLabel(string raw, out TimeOnly time)
    {
        if (TimeOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out time))
        {
            return true;
        }

        return TimeOnly.TryParse(raw, LabelFormat.ResolveReportCulture(), DateTimeStyles.None, out time);
    }
}
