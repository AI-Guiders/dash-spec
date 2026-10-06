using DashSpec.Core.Model;
using DashSpec.Execution.Runtime;
using DashSpec.Plugin.Viz.Builtins.Gantt;
using Xunit;

namespace DashSpec.Host.Tests;

public class GanttTimelineScaleTests
{
    [Fact]
    public void SummarizeRow_uses_segment_span_and_formats_duration()
    {
        var row = new GanttRow(
            "AutoCAD",
            [
                new GanttSegment(
                    new DateTime(2026, 9, 24, 9, 5, 0),
                    new DateTime(2026, 9, 24, 9, 10, 0),
                    "#ff0000",
                    "tooltip"),
            ]);

        var summary = GanttTimelineScale.SummarizeRow(row);

        Assert.Equal("AutoCAD", summary.Label);
        Assert.Equal(new DateTime(2026, 9, 24, 9, 5, 0), summary.Start);
        Assert.Equal(new DateTime(2026, 9, 24, 9, 10, 0), summary.End);
        Assert.Equal("5м", summary.DurationText);
    }

    [Fact]
    public void BuildTicks_intraday_uses_hour_grain()
    {
        var start = new DateTime(2026, 9, 24, 8, 0, 0);
        var end = new DateTime(2026, 9, 24, 18, 0, 0);
        var ticks = GanttTimelineScale.BuildTicks(start, end, "time.short");

        Assert.NotEmpty(ticks);
        Assert.True(ticks.Count >= 8);
    }

    [Fact]
    public void BuildTicks_weekly_aligns_to_report_culture_first_day_of_week()
    {
        LabelFormat.SetReportDefaults(new ReportFormatDefaults(Culture: "en-US"));
        try
        {
            var start = new DateTime(2026, 9, 24, 0, 0, 0);
            var end = new DateTime(2026, 10, 20, 0, 0, 0);
            var ticks = GanttTimelineScale.BuildTicks(start, end, "date.short");

            Assert.Contains(ticks, t => t.Label == "9/27");
        }
        finally
        {
            LabelFormat.ClearReportDefaults();
        }
    }

    [Fact]
    public void TodayLeftPercent_inside_axis_returns_percent()
    {
        var now = DateTime.UtcNow;
        var start = now.AddHours(-2);
        var end = now.AddHours(2);
        var left = GanttTimelineScale.TodayLeftPercent(start, end);

        Assert.NotNull(left);
        Assert.InRange(left.Value, 0, 100);
    }
}
