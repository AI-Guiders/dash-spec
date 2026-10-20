namespace DashSpec.Execution.Runtime;

/// <summary>
/// Mirror of the F# <c>serializeColumnLabelMap</c> IR encoding (TableDiagramParser):
/// newline-joined <c>id = label</c> lines, so commas inside a label survive the round-trip.
/// </summary>
internal static class ColumnLabelMap
{
    public static IReadOnlyDictionary<string, string> Parse(string? raw)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return map;
        }

        foreach (var line in raw.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = line.IndexOf('=');
            if (eq <= 0 || eq >= line.Length - 1)
            {
                continue;
            }

            var column = line[..eq].Trim();
            var label = line[(eq + 1)..].Trim();
            if (column.Length > 0 && label.Length > 0)
            {
                map[column] = label;
            }
        }

        return map;
    }
}
