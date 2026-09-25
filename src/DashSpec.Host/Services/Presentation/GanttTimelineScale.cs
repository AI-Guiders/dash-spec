using DashSpec.Execution.Runtime;

namespace DashSpec.Host.Services.Presentation;

public sealed record GanttTimelineTick(string Label, double LeftPercent, bool Major);

public static class GanttTimelineScale
{
    public static IReadOnlyList<GanttTimelineTick> BuildTicks(DateTime axisStart, DateTime axisEnd, string axisFormat)
    {
        if (axisEnd <= axisStart)
        {
            return [];
        }

        var span = axisEnd - axisStart;
        if (span.TotalHours <= 24)
        {
            return BuildHourlyTicks(axisStart, axisEnd, axisFormat);
        }

        if (span.TotalDays <= 14)
        {
            return BuildDailyTicks(axisStart, axisEnd);
        }

        return BuildWeeklyTicks(axisStart, axisEnd);
    }

    public static double? TodayLeftPercent(DateTime axisStart, DateTime axisEnd)
    {
        if (axisEnd <= axisStart)
        {
            return null;
        }

        var now = LabelFormat.ToDisplayTime(DateTime.UtcNow);
        if (now < axisStart || now > axisEnd)
        {
            return null;
        }

        var spanMinutes = (axisEnd - axisStart).TotalMinutes;
        if (spanMinutes <= 0)
        {
            return null;
        }

        return Math.Clamp((now - axisStart).TotalMinutes / spanMinutes * 100d, 0d, 100d);
    }

    public static int ResolveTrackMinWidthPx(DateTime axisStart, DateTime axisEnd)
    {
        var span = axisEnd - axisStart;
        if (span.TotalHours <= 24)
        {
            return (int)Math.Clamp(span.TotalHours * 56, 640, 2400);
        }

        if (span.TotalDays <= 14)
        {
            return (int)Math.Clamp(span.TotalDays * 48, 720, 2400);
        }

        return (int)Math.Clamp(span.TotalDays * 24, 960, 3200);
    }

    public static GanttRowSpan SummarizeRow(GanttRow row)
    {
        if (row.Segments.Count == 0)
        {
            return new GanttRowSpan(row.Label, null, null, "—");
        }

        var start = row.Segments.Min(segment => segment.Start);
        var end = row.Segments.Max(segment => segment.End);
        return new GanttRowSpan(
            row.Label,
            start,
            end,
            FormatDuration(end - start));
    }

    public static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes < 1)
        {
            return $"{Math.Max(1, (int)Math.Round(duration.TotalSeconds))}с";
        }

        if (duration.TotalHours < 1)
        {
            return $"{(int)Math.Round(duration.TotalMinutes)}м";
        }

        if (duration.TotalDays < 1)
        {
            var hours = (int)duration.TotalHours;
            var minutes = duration.Minutes;
            return minutes > 0 ? $"{hours}ч {minutes}м" : $"{hours}ч";
        }

        var days = (int)duration.TotalDays;
        var hoursLeft = duration.Hours;
        return hoursLeft > 0 ? $"{days}д {hoursLeft}ч" : $"{days}д";
    }

    private static IReadOnlyList<GanttTimelineTick> BuildHourlyTicks(
        DateTime axisStart,
        DateTime axisEnd,
        string axisFormat)
    {
        var spanMinutes = (axisEnd - axisStart).TotalMinutes;
        var ticks = new List<GanttTimelineTick>();
        var cursor = new DateTime(axisStart.Year, axisStart.Month, axisStart.Day, axisStart.Hour, 0, 0, axisStart.Kind);
        if (cursor < axisStart)
        {
            cursor = cursor.AddHours(1);
        }

        while (cursor <= axisEnd)
        {
            var left = (cursor - axisStart).TotalMinutes / spanMinutes * 100d;
            if (left >= 0 && left <= 100)
            {
                ticks.Add(new GanttTimelineTick(LabelFormat.FormatObject(cursor, axisFormat), left, cursor.Minute == 0));
            }

            cursor = cursor.AddHours(1);
        }

        if (ticks.Count == 0)
        {
            ticks.Add(new GanttTimelineTick(LabelFormat.FormatObject(axisStart, axisFormat), 0, true));
            ticks.Add(new GanttTimelineTick(LabelFormat.FormatObject(axisEnd, axisFormat), 100, true));
        }

        return ticks;
    }

    private static IReadOnlyList<GanttTimelineTick> BuildDailyTicks(DateTime axisStart, DateTime axisEnd)
    {
        var spanMinutes = (axisEnd - axisStart).TotalMinutes;
        var ticks = new List<GanttTimelineTick>();
        var cursor = axisStart.Date;
        while (cursor <= axisEnd)
        {
            var left = (cursor - axisStart).TotalMinutes / spanMinutes * 100d;
            if (left >= 0 && left <= 100)
            {
                ticks.Add(new GanttTimelineTick(cursor.ToString("dd.MM"), left, cursor.Day == 1));
            }

            cursor = cursor.AddDays(1);
        }

        return ticks;
    }

    private static IReadOnlyList<GanttTimelineTick> BuildWeeklyTicks(DateTime axisStart, DateTime axisEnd)
    {
        var spanMinutes = (axisEnd - axisStart).TotalMinutes;
        var ticks = new List<GanttTimelineTick>();
        var cursor = axisStart.Date;
        while (cursor.DayOfWeek != DayOfWeek.Monday && cursor > axisStart.Date.AddDays(-7))
        {
            cursor = cursor.AddDays(-1);
        }

        while (cursor <= axisEnd)
        {
            var left = (cursor - axisStart).TotalMinutes / spanMinutes * 100d;
            if (left >= 0 && left <= 100)
            {
                ticks.Add(new GanttTimelineTick(cursor.ToString("dd.MM"), left, true));
            }

            cursor = cursor.AddDays(7);
        }

        return ticks;
    }
}

public sealed record GanttRowSpan(string Label, DateTime? Start, DateTime? End, string DurationText);
