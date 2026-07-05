using DashSpec.Core.Model;

namespace DashSpec.Core.Runtime;

internal static class TablePayloadBuilder
{
    public static TablePayload Build(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        DiagramDefinition diagram)
    {
        var columns = diagram.Properties.TryGetValue("columns", out var raw)
            ? raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            : rows.FirstOrDefault()?.Keys.ToArray() ?? [];

        var tableRows = rows
            .Select(row => columns
                .Select(column => PayloadRowFormatters.FormatValue(row.GetValueOrDefault(column)))
                .ToList())
            .ToList();

        return new TablePayload(columns.ToList(), tableRows);
    }
}
