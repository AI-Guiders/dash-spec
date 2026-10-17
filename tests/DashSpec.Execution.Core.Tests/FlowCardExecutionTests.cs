using DashSpec.Abstractions.Data;
using DashSpec.Core.Model;
using DashSpec.Core.Transforms;
using DashSpec.Execution.Runtime;
using Xunit;

namespace DashSpec.Execution.Core.Tests;

public sealed class FlowCardExecutionTests
{
    private static readonly DashflowModuleDefinition Flow = new(
        "peak",
        [
            new DashflowSourceDefinition(
                "utilization",
                ProviderInfer: false,
                ProviderId: "postgres",
                DashflowSourceFromKind.View,
                "demo.v_daily_peak",
                "utilization",
                "UtilizationRow"),
        ],
        [
            new DashflowTransformerDefinition(
                "reporting_calendar",
                [new DashflowTransformerPortDefinition("localized", "UtilizationRowLocalized")],
                DefaultInputPort: "raw",
                DefaultOutputPort: "localized",
                Steps: []),
        ],
        [
                new DashflowLinkDefinition(
                    "utilization",
                    Microsoft.FSharp.Core.FSharpOption<string>.Some("utilization"),
                    "reporting_calendar",
                    Microsoft.FSharp.Core.FSharpOption<string>.Some("raw")),
        ]);

    [Fact]
    public void ApplyFlowInput_materializes_view_datasource_from_source_port()
    {
        var document = new DashboardDocument(
            "t",
            "T",
            null,
            SqlDialect.TSql,
            null,
            null,
            null,
            LayoutDefinition.Default,
                    TestModelDefaults.FiltersChrome,
            [],
            [],
            [],
            [
                CardTestData.Create(
                    "c",
                    "C",
                    new DiagramDefinition("table", new Dictionary<string, string>(), null),
                    DashSpec.Core.Parsing.DataSourceDefinitionFactory.Create(DataSourceKind.View, string.Empty, rowsType: string.Empty),
                    [],
                    [],
                    flowInput: new CardFlowInputDefinition("rows", "utilization", "utilization")),
            ],
            Dashflow: Flow);

        var resolved = FlowCardExecution.ApplyFlowInput(document.Cards[0], document);

        Assert.Equal("demo.v_daily_peak", resolved.DataSource.Value);
        Assert.Equal("UtilizationRow", resolved.DataSource.RowsType);
    }

    [Fact]
    public void ResolveProviderId_returns_named_source_provider()
    {
        var card = CardTestData.Create(
            "c",
            "C",
            new DiagramDefinition("table", new Dictionary<string, string>(), null),
            DashSpec.Core.Parsing.DataSourceDefinitionFactory.Create(DataSourceKind.View, string.Empty),
            [],
            [],
            flowInput: new CardFlowInputDefinition("rows", "utilization", "utilization"));

        var document = new DashboardDocument(
            "t",
            "T",
            null,
            SqlDialect.TSql,
            null,
            null,
            null,
            LayoutDefinition.Default,
                TestModelDefaults.FiltersChrome,
            [],
            [],
            [],
            [card],
            Dashflow: Flow);

        var materialized = DocumentFlowBinder.MaterializeCard(card, document);
        Assert.Equal("postgres", FlowCardExecution.ResolveProviderId(materialized));
    }

    [Fact]
    public void ApplyFlowInput_uses_transformer_output_row_type()
    {
        var document = new DashboardDocument(
            "t",
            "T",
            null,
            SqlDialect.TSql,
            null,
            null,
            null,
            LayoutDefinition.Default,
                TestModelDefaults.FiltersChrome,
            [],
            [],
            [],
            [
                CardTestData.Create(
                    "c",
                    "C",
                    new DiagramDefinition("table", new Dictionary<string, string>(), null),
                    DashSpec.Core.Parsing.DataSourceDefinitionFactory.Create(DataSourceKind.View, string.Empty, rowsType: string.Empty),
                    [],
                    [],
                    flowInput: new CardFlowInputDefinition("rows", "reporting_calendar", "localized")),
            ],
            Dashflow: Flow);

        var resolved = FlowCardExecution.ApplyFlowInput(document.Cards[0], document);

        Assert.Equal("UtilizationRowLocalized", resolved.DataSource.RowsType);
        Assert.Equal("demo.v_daily_peak", resolved.DataSource.Value);
    }

    [Fact]
    public void CardDataPipeline_runs_to_zone_on_transformer_path()
    {
        var flow = Flow with
        {
            Transformers =
            [
                new DashflowTransformerDefinition(
                    "reporting_calendar",
                    [new DashflowTransformerPortDefinition("localized", "UtilizationRowLocalized")],
                    DefaultInputPort: "raw",
                    DefaultOutputPort: "localized",
                    Steps:
                    [
                        new DashflowTransformStepDefinition(
                            BuiltinScalarTransformIds.ToZone,
                            [new DashflowTransformParameterDefinition("offset_minutes", "180")]),
                    ]),
            ],
        };

        var document = new DashboardDocument(
            "t",
            "T",
            null,
            SqlDialect.TSql,
            null,
            null,
            null,
            LayoutDefinition.Default,
                    TestModelDefaults.FiltersChrome,
            [],
            [],
            [],
            [
                CardTestData.Create(
                    "c",
                    "C",
                    new DiagramDefinition("table", new Dictionary<string, string>(), null),
                    DashSpec.Core.Parsing.DataSourceDefinitionFactory.Create(DataSourceKind.View, string.Empty, rowsType: string.Empty),
                    [],
                    [],
                    flowInput: new CardFlowInputDefinition("rows", "reporting_calendar", "localized")),
            ],
            Dashflow: flow);

        var card = DocumentFlowBinder.MaterializeCard(document.Cards[0], document);
        var schema = new RowTypeSchema(
            "UtilizationRowLocalized",
            [new RowFieldSchema("bucket_start_utc", DashPrimitiveKind.DateTime)]);
        var utc = new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc);
        var batch = TypedRowBatch.Create(
            schema,
            new List<DashValue[]>
            {
                new[] { DashValue.FromDateTimeUtc(utc) },
            });

        var transformed = DashflowCardDataPipeline.ApplyTransforms(document, card, batch);

        Assert.Equal(12, transformed[0].Get("bucket_start_utc").AsDateTimeUtc().Hour);
        Assert.True(transformed[0].Get("bucket_start_utc").IsDisplayWallClockDateTime);
    }

    [Fact]
    public void MaterializeCard_materializes_per_slot_flow_inputs()
    {
        var flow = Flow with
        {
            Sources =
            [
                Flow.Sources[0],
                new DashflowSourceDefinition(
                    "drill_src",
                    ProviderInfer: false,
                    ProviderId: "postgres",
                    DashflowSourceFromKind.View,
                    "demo.v_drill",
                    "rows",
                    "UtilizationRow"),
            ],
        };

        var slots = new Dictionary<string, CardDiagramSlotDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["heatmap"] = new CardDiagramSlotDefinition
            {
                SlotRef = "heatmap",
                Diagram = new DiagramDefinition("heatmap", new Dictionary<string, string>(), null),
                DataSource = DashSpec.Core.Parsing.DataSourceDefinitionFactory.Create(DataSourceKind.View, string.Empty),
                BoundFilters = [],
                Legend = Microsoft.FSharp.Core.FSharpOption<LegendDefinition>.None,
                Presentation = Microsoft.FSharp.Core.FSharpOption<PresentationBlock>.None,
                SeriesTransform = Microsoft.FSharp.Core.FSharpOption<SeriesTransformBlock>.None,
                FlowInput = Microsoft.FSharp.Core.FSharpOption<CardFlowInputDefinition>.Some(new CardFlowInputDefinition("rows", "utilization", "utilization")),
            },
            ["drill"] = new CardDiagramSlotDefinition
            {
                SlotRef = "drill",
                Diagram = new DiagramDefinition("table", new Dictionary<string, string>(), null),
                DataSource = DashSpec.Core.Parsing.DataSourceDefinitionFactory.Create(DataSourceKind.View, string.Empty),
                BoundFilters = [],
                Legend = Microsoft.FSharp.Core.FSharpOption<LegendDefinition>.None,
                Presentation = Microsoft.FSharp.Core.FSharpOption<PresentationBlock>.None,
                SeriesTransform = Microsoft.FSharp.Core.FSharpOption<SeriesTransformBlock>.None,
                FlowInput = Microsoft.FSharp.Core.FSharpOption<CardFlowInputDefinition>.Some(new CardFlowInputDefinition("rows", "drill_src", "rows")),
            },
        };

        var card = CardTestData.Create(
            "c",
            "C",
            slots["heatmap"].Diagram,
            DashSpec.Core.Parsing.DataSourceDefinitionFactory.Create(DataSourceKind.View, string.Empty),
            [],
            [],
            diagramSlotRef: "heatmap",
            diagramSlots: slots,
            flowInput: new CardFlowInputDefinition("rows", "utilization", "utilization"));

        var document = new DashboardDocument(
            "t",
            "T",
            null,
            SqlDialect.TSql,
            null,
            null,
            null,
            LayoutDefinition.Default,
            TestModelDefaults.FiltersChrome,
            [],
            [],
            [],
            [card],
            Dashflow: flow);

        var materialized = DocumentFlowBinder.MaterializeCard(card, document);

        Assert.Equal("demo.v_daily_peak", materialized.DiagramSlots!["heatmap"].DataSource.Value);
        Assert.Equal("demo.v_drill", materialized.DiagramSlots!["drill"].DataSource.Value);
        Assert.Equal("demo.v_daily_peak", materialized.DataSource.Value);
    }
}
