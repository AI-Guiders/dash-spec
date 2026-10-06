using DashSpec.Abstractions.Data;
using DashSpec.Core.Model;
using DashSpec.Core.Transforms;
using DashSpec.Execution.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class DashflowBatchExecutorTests
{
    private static readonly RowTypeSchema Schema = new(
        "PeakRow",
        [
            new RowFieldSchema("bucket_start_utc", DashPrimitiveKind.DateTime),
            new RowFieldSchema("peak_value", DashPrimitiveKind.Int),
        ]);

    [Fact]
    public void ToZone_shifts_datetime_columns_by_offset_minutes()
    {
        var utc = new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc);
        var batch = TypedRowBatch.Create(
            Schema,
            new List<DashValue[]>
            {
                new[] { DashValue.FromDateTimeUtc(utc), DashValue.FromInt(3) },
            });

        var transformed = ToZoneBatchTransform.Apply(
            batch,
            [
                new DashflowTransformParameterDefinition("zone", "UTC+3"),
                new DashflowTransformParameterDefinition("offset_minutes", "180"),
            ]);

        var shifted = transformed[0].Get("bucket_start_utc").AsDateTimeUtc();
        Assert.Equal(12, shifted.Hour);
        Assert.Equal(15, shifted.Day);
    }

    [Fact]
    public void Executor_runs_to_zone_on_transformer_chain()
    {
        var utc = new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc);
        var batch = TypedRowBatch.Create(
            Schema,
            new List<DashValue[]>
            {
                new[] { DashValue.FromDateTimeUtc(utc), DashValue.FromInt(1) },
            });

        IReadOnlyList<DashflowTransformerDefinition> transformers =
        [
            new DashflowTransformerDefinition(
                "local",
                new[] { new DashflowTransformerPortDefinition("out", "PeakRow") },
                "in",
                "out",
                new[]
                {
                    new DashflowTransformStepDefinition(
                        BuiltinScalarTransformIds.ToZone,
                        new[] { new DashflowTransformParameterDefinition("offset_minutes", "180") }),
                }),
        ];

        var result = DashflowBatchExecutor.Apply(batch, transformers);
        Assert.Equal(12, result[0].Get("bucket_start_utc").AsDateTimeUtc().Hour);
    }

    [Fact]
    public void ToZone_marks_cells_as_display_wall_clock_for_present_layer()
    {
        var utc = new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc);
        var batch = TypedRowBatch.Create(
            Schema,
            new List<DashValue[]>
            {
                new[] { DashValue.FromDateTimeUtc(utc), DashValue.FromInt(1) },
            });

        var transformed = ToZoneBatchTransform.Apply(
            batch,
            [new DashflowTransformParameterDefinition("offset_minutes", "180")]);

        var cell = transformed[0].Get("bucket_start_utc");
        Assert.True(cell.IsDisplayWallClockDateTime);
        Assert.IsType<DashDisplayDateTime>(cell.ToClr());
    }

    [Fact]
    public void Display_wall_clock_is_not_shifted_again_when_host_display_tz_set()
    {
        var moscow = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Russian Standard Time" : "Europe/Moscow");
        LabelFormat.DisplayTimeZone = moscow;
        try
        {
            var display = new DashDisplayDateTime(new DateTime(2026, 1, 15, 12, 0, 0));
            Assert.Equal("12:00", LabelFormat.FormatObject(display, "HH:mm"));
        }
        finally
        {
            LabelFormat.DisplayTimeZone = null;
        }
    }
}
