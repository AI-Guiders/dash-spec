using System.Globalization;
using DashSpec.Core.Model;

namespace DashSpec.Core.Runtime;

public static class KpiPriorPeriod
{
    public static bool WantsPriorDelta(DiagramDefinition diagram) =>
        diagram.Properties.TryGetValue("delta", out var delta) &&
        delta is not null &&
        (delta.Equals("prior", StringComparison.OrdinalIgnoreCase) ||
         delta.Equals("previous", StringComparison.OrdinalIgnoreCase) ||
         delta.Equals("prior_period", StringComparison.OrdinalIgnoreCase));

    public static bool TryBuildPriorFilters(
        IEnumerable<string> filterNames,
        FilterState filters,
        IReadOnlyDictionary<string, FilterDefinition> filterIndex,
        out FilterState priorFilters)
    {
        ArgumentNullException.ThrowIfNull(filterNames);
        ArgumentNullException.ThrowIfNull(filters);
        ArgumentNullException.ThrowIfNull(filterIndex);

        foreach (var name in filterNames)
        {
            if (string.IsNullOrWhiteSpace(name) ||
                !filterIndex.TryGetValue(name, out var definition) ||
                definition.Kind != FilterKind.Date)
            {
                continue;
            }

            var range = filters.GetDate(name);
            if (range is null)
            {
                continue;
            }

            var days = range.Value.To.DayNumber - range.Value.From.DayNumber + 1;
            if (days <= 0)
            {
                continue;
            }

            var priorTo = range.Value.From.AddDays(-1);
            var priorFrom = priorTo.AddDays(-(days - 1));
            priorFilters = filters.Clone();
            priorFilters.SetDate(name, priorFrom, priorTo);
            return true;
        }

        priorFilters = filters;
        return false;
    }

    public static bool TryReadScalar(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        DiagramDefinition diagram,
        out double value)
    {
        value = 0;
        if (rows.Count == 0 ||
            !DiagramBindings.TryGetColumn(diagram, "value", out var column))
        {
            return false;
        }

        return MeasureValues.TryReadDouble(rows[0].GetValueOrDefault(column), out value);
    }

    public static (string Text, string Tone) FormatDelta(double current, double prior)
    {
        var culture = CultureInfo.CurrentCulture;
        var absolute = current - prior;
        var tone = absolute switch
        {
            > 0 => "up",
            < 0 => "down",
            _ => "flat",
        };

        var arrow = tone switch
        {
            "up" => "↑",
            "down" => "↓",
            _ => "→",
        };

        if (Math.Abs(prior) < 1e-12)
        {
            if (Math.Abs(absolute) < 1e-12)
            {
                return ($"{arrow} 0 vs prior", "flat");
            }

            var absoluteText = absolute.ToString("+#,##0.##;-#,##0.##", culture);
            return ($"{arrow} {absoluteText} vs prior", tone);
        }

        var percent = absolute / Math.Abs(prior) * 100d;
        var percentText = percent.ToString("+0.#;-0.#", culture) + "%";
        return ($"{arrow} {percentText} vs prior", tone);
    }
}
