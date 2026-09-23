using System.Globalization;
using DashSpec.Core.Model;

namespace DashSpec.Execution.Runtime;

internal static class GanttPayloadBuilder
{
    private const string DefaultBarColor = "#4c78a8";

    public static GanttPayload Build(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        DiagramDefinition diagram)
    {
        var yColumn = DiagramBindings.Column(diagram, "y");
        var startColumn = DiagramBindings.Column(diagram, "from");
        var endColumn = DiagramBindings.Column(diagram, "to");
        DiagramBindings.TryGetColumn(diagram, "color", out var colorColumn);

        var heightPx = 360;
        if (diagram.Properties.TryGetValue("height", out var heightRaw) &&
            int.TryParse(heightRaw, out var parsedHeight) &&
            parsedHeight is > 80 and < 2000)
        {
            heightPx = parsedHeight;
        }

        var rowMap = new Dictionary<string, List<GanttSegment>>(StringComparer.OrdinalIgnoreCase);
        var axisStart = DateTime.MaxValue;
        var axisEnd = DateTime.MinValue;

        foreach (var row in rows)
        {
            if (!TryReadDateTime(row.GetValueOrDefault(startColumn), out var start) ||
                !TryReadDateTime(row.GetValueOrDefault(endColumn), out var end) ||
                end <= start)
            {
                continue;
            }

            var label = PayloadRowFormatters.FormatValue(row.GetValueOrDefault(yColumn)) ?? "—";
            var color = ResolveColor(row.GetValueOrDefault(colorColumn));
            var tooltip = $"{label}: {start:HH:mm} – {end:HH:mm}";

            if (!rowMap.TryGetValue(label, out var segments))
            {
                segments = [];
                rowMap[label] = segments;
            }

            segments.Add(new GanttSegment(start, end, color, tooltip));
            axisStart = start < axisStart ? start : axisStart;
            axisEnd = end > axisEnd ? end : axisEnd;
        }

        if (rowMap.Count == 0)
        {
            return new GanttPayload([], DateTime.UtcNow, DateTime.UtcNow, DiagramBindings.Label(diagram, "y"), heightPx);
        }

        if (axisEnd <= axisStart)
        {
            axisEnd = axisStart.AddMinutes(5);
        }

        var spanMinutes = Math.Max((axisEnd - axisStart).TotalMinutes, 1d);
        var ganttRows = rowMap
            .Select(pair =>
            {
                var positioned = pair.Value
                    .OrderBy(segment => segment.Start)
                    .Select(segment => PositionSegment(segment, axisStart, spanMinutes))
                    .ToList();
                return new GanttRow(pair.Key, positioned);
            })
            .OrderBy(row => row.Segments.Count > 0 ? row.Segments[0].Start : axisStart)
            .ThenBy(row => row.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new GanttPayload(
            ganttRows,
            axisStart,
            axisEnd,
            DiagramBindings.Label(diagram, "y"),
            heightPx);
    }

    private static GanttSegment PositionSegment(GanttSegment segment, DateTime axisStart, double spanMinutes)
    {
        var left = (segment.Start - axisStart).TotalMinutes / spanMinutes * 100d;
        var width = (segment.End - segment.Start).TotalMinutes / spanMinutes * 100d;
        left = Math.Clamp(left, 0d, 100d);
        width = Math.Clamp(width, 0.4d, 100d - left);
        return segment with
        {
            LeftPercent = left,
            WidthPercent = width,
        };
    }

    private static string ResolveColor(object? raw)
    {
        var text = Convert.ToString(raw, CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(text) ? DefaultBarColor : text.Trim();
    }

    private static bool TryReadDateTime(object? raw, out DateTime value)
    {
        switch (raw)
        {
            case DateTime dt:
                value = DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);
                return true;
            case DateTimeOffset dto:
                value = dto.UtcDateTime;
                return true;
            case DateOnly date:
                value = date.ToDateTime(TimeOnly.MinValue);
                return true;
            case string text when DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed):
                value = parsed;
                return true;
            default:
                value = default;
                return false;
        }
    }
}

public sealed record GanttPayload(
    IReadOnlyList<GanttRow> Rows,
    DateTime AxisStart,
    DateTime AxisEnd,
    string? YLabel,
    int HeightPx);

public sealed record GanttRow(string Label, IReadOnlyList<GanttSegment> Segments);

public sealed record GanttSegment(
    DateTime Start,
    DateTime End,
    string Color,
    string Tooltip,
    double LeftPercent = 0,
    double WidthPercent = 0);
