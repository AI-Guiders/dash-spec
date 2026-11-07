using DashSpec.Abstractions.Data;
using DashSpec.Execution.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

/// <summary>ADR-0120: the reduce transform plugin — runtime face of `series max`.</summary>
public sealed class ReduceBatchTransformTests
{
    private static readonly RowTypeSchema Schema = new(
        "ActivityRow",
        [
            new RowFieldSchema("bucket_start_utc", DashPrimitiveKind.DateTime),
            new RowFieldSchema("app_name", DashPrimitiveKind.String),
            new RowFieldSchema("value", DashPrimitiveKind.Int),
        ]);

    private static TypedRowBatch Batch(params (string app, int value)[] rows)
    {
        var utc = new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc);
        return TypedRowBatch.Create(
            Schema,
            rows.Select(r => new DashValue[]
            {
                DashValue.FromDateTimeUtc(utc),
                DashValue.FromString(r.app),
                DashValue.FromInt(r.value),
            }).ToList());
    }

    private static readonly IReadOnlyList<DashflowTransformParameterDefinition> Params =
    [
        new DashflowTransformParameterDefinition("by", "app_name"),
        new DashflowTransformParameterDefinition("top", "2"),
        new DashflowTransformParameterDefinition("tail", "Другие"),
    ];

    [Fact]
    public void Top_groups_survive_and_the_tail_folds_into_one_label()
    {
        var batch = Batch(
            ("nanoCAD", 50), ("nanoCAD", 10),
            ("AliveColors", 30),
            ("DeadOne", 5), ("DeadTwo", 3), ("DeadThree", 1));

        var reduced = ReduceBatchTransform.Apply(batch, Params);

        // weight ranking: nanoCAD 60, AliveColors 30 survive; the rest folds
        var apps = reduced.Rows.Select(r => r.Get("app_name").ToClr()?.ToString()).ToList();
        Assert.Equal(2, apps.Count(a => a == "nanoCAD"));            // both rows intact
        Assert.Single(apps, a => a == "AliveColors");
        Assert.Equal(3, apps.Count(a => a == "Другие"));             // 3 dead rows folded into the tail
    }

    [Fact]
    public void Without_value_column_the_weight_is_the_row_count()
    {
        var schema = new RowTypeSchema("S", [new RowFieldSchema("app_name", DashPrimitiveKind.String)]);
        var batch = TypedRowBatch.Create(
            schema,
            Enumerable.Range(0, 10).Select(i => new DashValue[] { DashValue.FromString(i < 7 ? "big" : $"small{i}") }).ToList());

        var reduced = ReduceBatchTransform.Apply(batch, [new DashflowTransformParameterDefinition("by", "app_name"), new DashflowTransformParameterDefinition("top", "1")]);

        // big (7 rows) survives; the three smalls fold into the default tail label
        var apps = reduced.Rows.Select(r => r.Get("app_name").ToClr()?.ToString()).ToList();
        Assert.Equal(7, apps.Count(a => a == "big"));
        Assert.Equal(3, apps.Count(a => a == ReduceBatchTransform.DefaultTailLabel));
    }

    [Fact]
    public void Executor_runs_the_reduce_step_in_a_transformer_chain()
    {
        var transformer = new DashflowTransformerDefinition(
            "card.peak.reduce",
            [],
            [System.Tuple.Create("rows", "ActivityRow")],
            [],
            Microsoft.FSharp.Core.FSharpOption<string>.Some("rows"),
            Microsoft.FSharp.Core.FSharpOption<string>.Some("rows"),
            [new DashflowTransformStepDefinition("reduce", [
                new DashflowTransformParameterDefinition("by", "app_name"),
                new DashflowTransformParameterDefinition("top", "1"),
                new DashflowTransformParameterDefinition("tail", "Другие")])]);

        var reduced = DashflowBatchExecutor.Apply(Batch(("a", 5), ("b", 1)), [transformer]);

        Assert.Equal(2, reduced.Count);
        Assert.Equal("a", reduced[0].Get("app_name").ToClr()?.ToString());
        Assert.Equal(ReduceBatchTransform.DefaultTailLabel, reduced[1].Get("app_name").ToClr()?.ToString());
    }

    [Fact]
    public void Missing_by_is_a_loud_runtime_error()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ReduceBatchTransform.Apply(Batch(("a", 1)), [new DashflowTransformParameterDefinition("top", "2")]));
        Assert.Contains("by", ex.Message);
    }

    [Fact]
    public void Empty_tail_label_drops_the_tail_groups()
    {
        var reduced = ReduceBatchTransform.Apply(
            Batch(("a", 5), ("b", 1)),
            [
                new DashflowTransformParameterDefinition("by", "app_name"),
                new DashflowTransformParameterDefinition("top", "1"),
                new DashflowTransformParameterDefinition("tail", ""),
            ]);

        Assert.Single(reduced.Rows);
        Assert.Equal("a", reduced[0].Get("app_name").ToClr()?.ToString());
    }
}
