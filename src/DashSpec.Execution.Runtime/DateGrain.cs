namespace DashSpec.Execution.Runtime;

/// <summary>Date-domain selector grain (ADR-0118). The grain word lives only at the
/// DSL edge (filter values, defaults block); everything downstream — the widget seam,
/// the stack controls — speaks this enum. Quarter joins when command-level periods do.</summary>
public enum DateGrain
{
    Day,
    Month,
    Year,
}

/// <summary>DSL-edge parsing: the only place grain words become the enum.</summary>
public static class DateGrainText
{
    public static bool TryParse(string? value, out DateGrain grain)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "day":
                grain = DateGrain.Day;
                return true;
            case "month":
                grain = DateGrain.Month;
                return true;
            case "year":
                grain = DateGrain.Year;
                return true;
            default:
                grain = DateGrain.Day;
                return false;
        }
    }
}
