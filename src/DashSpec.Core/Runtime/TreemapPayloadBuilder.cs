using DashSpec.Core.Model;

namespace DashSpec.Core.Runtime;

internal static class TreemapPayloadBuilder
{
    public static ChartPayload Build(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        DiagramDefinition diagram)
    {
        var categoryColumn = DiagramBindings.Column(diagram, "x");
        var valueColumn = DiagramBindings.Column(diagram, "y");
        var tiles = new List<TreemapTile>();

        foreach (var row in rows)
        {
            if (!MeasureValues.TryReadDouble(row.GetValueOrDefault(valueColumn), out var value) ||
                value <= 0)
            {
                continue;
            }

            var raw = row.GetValueOrDefault(categoryColumn);
            var label = raw is null or DBNull
                ? "(null)"
                : Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture) ?? "(null)";
            tiles.Add(new TreemapTile(label, value));
        }

        if (tiles.Count == 0)
        {
            return new ChartPayload([], []);
        }

        tiles.Sort((a, b) => b.Value.CompareTo(a.Value));
        var labels = tiles.Select(t => t.Label).ToList();
        var values = tiles.Select(t => (double?)t.Value).ToList();
        var seriesLabel = DiagramBindings.Label(diagram, "y") ?? valueColumn;

        return new ChartPayload(
            labels,
            [new ChartSeries(seriesLabel, values)],
            Treemap: tiles);
    }
}
