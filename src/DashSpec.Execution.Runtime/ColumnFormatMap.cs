namespace DashSpec.Execution.Runtime;

internal static class ColumnFormatMap
{
    public static IReadOnlyDictionary<string, string> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = entry.IndexOf(':');
            if (colon <= 0 || colon >= entry.Length - 1)
            {
                continue;
            }

            var column = entry[..colon].Trim();
            var format = entry[(colon + 1)..].Trim();
            if (column.Length > 0 && format.Length > 0)
            {
                map[column] = format;
            }
        }

        return map;
    }
}
