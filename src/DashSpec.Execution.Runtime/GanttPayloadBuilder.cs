using System.Globalization;
using DashSpec.Core.Model;

namespace DashSpec.Execution.Runtime;

internal static class GanttPayloadBuilder
{
    private const string DefaultBarColor = "#4c78a8";
    private const string DefaultDateColumn = "usage_date";
    private static readonly TimeSpan DefaultPollStep = TimeSpan.FromMinutes(5);

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

        var step = ResolveStep(diagram);
        var rowMap = new Dictionary<string, List<GanttSegment>>(StringComparer.OrdinalIgnoreCase);
        var hasFixedAxis = TryResolveFixedAxis(rows, diagram, out var axisStart, out var axisEnd, step);
        if (!hasFixedAxis)
        {
            axisStart = DateTime.MaxValue;
            axisEnd = DateTime.MinValue;
        }

        foreach (var row in rows)
        {
            if (!TryReadDateTime(row.GetValueOrDefault(startColumn), out var startUtc) ||
                !TryReadDateTime(row.GetValueOrDefault(endColumn), out var endUtc) ||
                endUtc <= startUtc)
            {
                continue;
            }

            var start = LabelFormat.ToDisplayTime(startUtc);
            var end = LabelFormat.ToDisplayTime(endUtc);
            if (hasFixedAxis && (start >= axisEnd || end <= axisStart))
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
            if (!hasFixedAxis)
            {
                axisStart = start < axisStart ? start : axisStart;
                axisEnd = end > axisEnd ? end : axisEnd;
            }
        }

        if (rowMap.Count == 0)
        {
            return new GanttPayload([], axisStart, axisEnd, DiagramBindings.Label(diagram, "y"), heightPx);
        }

        if (!hasFixedAxis)
        {
            if (axisEnd <= axisStart)
            {
                axisEnd = axisStart.Add(step);
            }
        }

        var spanMinutes = Math.Max((axisEnd - axisStart).TotalMinutes, 1d);
        var ganttRows = rowMap
            .Select(pair =>
            {
                var positioned = pair.Value
                    .OrderBy(segment => segment.Start)
                    .Select(segment => PositionSegment(segment, axisStart, spanMinutes, step))
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

    private static TimeSpan ResolveStep(DiagramDefinition diagram)
    {
        if (diagram.Properties.TryGetValue("step", out var stepRaw) &&
            TimeSeriesGrid.TryParseStep(stepRaw, out var parsedStep))
        {
            return parsedStep;
        }

        if (diagram.Properties.TryGetValue("poll_interval_seconds", out var pollRaw) &&
            int.TryParse(pollRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pollSeconds) &&
            pollSeconds > 0)
        {
            return TimeSpan.FromSeconds(pollSeconds);
        }

        return DefaultPollStep;
    }

    private static bool TryResolveFixedAxis(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        DiagramDefinition diagram,
        out DateTime axisStart,
        out DateTime axisEnd,
        TimeSpan step)
    {
        axisStart = default;
        axisEnd = default;

        if (!diagram.Properties.TryGetValue("axis_from", out var fromRaw) ||
            !diagram.Properties.TryGetValue("axis_to", out var toRaw) ||
            !TimeOnly.TryParse(fromRaw.Trim(), CultureInfo.InvariantCulture, out var fromTime) ||
            !TimeOnly.TryParse(toRaw.Trim(), CultureInfo.InvariantCulture, out var toTime))
        {
            return false;
        }

        var dateColumn = diagram.Properties.GetValueOrDefault("date_column") ?? DefaultDateColumn;
        var anchorDate = ResolveAnchorDate(rows, dateColumn);
        if (anchorDate is null)
        {
            return false;
        }

        axisStart = anchorDate.Value.ToDateTime(fromTime, DateTimeKind.Unspecified);
        axisEnd = anchorDate.Value.ToDateTime(toTime, DateTimeKind.Unspecified);
        if (axisEnd <= axisStart)
        {
            axisEnd = axisStart.Add(step);
        }

        return true;
    }

    private static DateOnly? ResolveAnchorDate(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        string dateColumn)
    {
        foreach (var row in rows)
        {
            if (!row.TryGetValue(dateColumn, out var raw))
            {
                continue;
            }

            switch (raw)
            {
                case DateOnly date:
                    return date;
                case DateTime dt:
                    return DateOnly.FromDateTime(dt);
                case string text when DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed):
                    return parsed;
            }
        }

        return null;
    }

    private static GanttSegment PositionSegment(
        GanttSegment segment,
        DateTime axisStart,
        double spanMinutes,
        TimeSpan step)
    {
        var left = (segment.Start - axisStart).TotalMinutes / spanMinutes * 100d;
        var width = step.TotalMinutes / spanMinutes * 100d;
        left = Math.Clamp(left, 0d, 100d);
        width = Math.Clamp(width, 0.75d, 100d - left);
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
                value = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                return true;
            case DateTimeOffset dto:
                value = dto.UtcDateTime;
                return true;
            case DateOnly date:
                value = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
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
