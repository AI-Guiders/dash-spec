using DashSpec.Core.Model;
using DashSpec.Core.Runtime;
using DashSpec.Execution.Runtime;

namespace DashSpec.Host.Services.Presentation;

internal static class HeatmapCellFilterResolver
{
    public static CardCellDrillOverlay BuildOverlay(
        IEnumerable<SetFilterFromFieldEffect> binds,
        HeatmapCellContext context,
        IReadOnlyDictionary<string, FilterDefinition> filterIndex,
        FilterState filters)
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
                    overlay.SetField(bind.FilterName, raw);
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
        Action<string, string> setField)
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
                setField(effect.FilterName, raw);
                break;
        }
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
}
