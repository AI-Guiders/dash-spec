using DashSpec.Abstractions.Data;
using DashSpec.Execution.Runtime;
using Xunit;

namespace DashSpec.Execution.Core.Tests;
public sealed class FlowCardExecutionTests
{
    private static readonly DashflowModuleDefinition Flow = CreatePeakFlow();

    private static DashflowModuleDefinition CreatePeakFlow()
    {
        var source = Source("utilization", "demo.v_daily_peak", "utilization", "UtilizationRow");
        var transformer = Transformer("reporting_calendar", "raw", "localized");
        var link = new DashflowLinkDefinition(
            "utilization",
            Microsoft.FSharp.Core.FSharpOption<string>.Some("utilization"),
            "reporting_calendar",
            Microsoft.FSharp.Core.FSharpOption<string>.Some("raw"));

        return CreateModule([source], [transformer], [link]);
    }

    private static DashflowSourceDefinition Source(
        string id,
        string fromValue,
        string outputPort,
        string outputRowType) =>
        new(
            id,
            DashSpec.Modeling.Parse.DataFlow.DashflowProviderBinding.NewNamed("postgres"),
            new DashSpec.Modeling.Parse.DataFlow.SourceFrom(
                DashflowSourceFromKind.View,
                fromValue,
                Microsoft.FSharp.Core.FSharpOption<string>.None),
            outputPort,
            outputRowType,
            Microsoft.FSharp.Core.FSharpOption<string>.None);

    private static DashflowTransformerDefinition Transformer(
        string id,
        string defaultInputPort,
        string defaultOutputPort,
        params DashflowTransformStepDefinition[] steps) =>
        new(
            id,
            [],
            [System.Tuple.Create("localized", "UtilizationRowLocalized")],
            Microsoft.FSharp.Core.FSharpOption<string>.Some(defaultInputPort),
            Microsoft.FSharp.Core.FSharpOption<string>.Some(defaultOutputPort),
            steps);

    private static DashflowModuleDefinition CreateModule(
        DashflowSourceDefinition[] sources,
        DashflowTransformerDefinition[] transformers,
        DashflowLinkDefinition[] links) =>
        new(
            "peak",
            sources,
            transformers,
            [],
            links,
            DashSpec.Modeling.Core.FlowGraphModule.empty,
            []);

    [Fact]
    public void ApplyFlowInput_materializes_view_datasource_from_source_port()
    {
        var document = DocumentTestData.Create(
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
                    DashSpec.Execution.Parsing.DataSourceDefinitionFactory.Create(DataSourceKind.View, string.Empty, rowsType: string.Empty),
                    [],
                    [],
                    flowInput: new CardFlowInputDefinition("rows", "utilization", "utilization")),
            ],
            dashflow: Flow);
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
            DashSpec.Execution.Parsing.DataSourceDefinitionFactory.Create(DataSourceKind.View, string.Empty),
            [],
            [],
            flowInput: new CardFlowInputDefinition("rows", "utilization", "utilization"));

        var document = DocumentTestData.Create(
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
            dashflow: Flow);

        var materialized = DocumentFlowBinder.MaterializeCard(card, document);
        Assert.Equal("postgres", FlowCardExecution.ResolveProviderId(materialized));
    }

    [Fact]
    public void ApplyFlowInput_uses_transformer_output_row_type()
    {
        var document = DocumentTestData.Create(
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
                    DashSpec.Execution.Parsing.DataSourceDefinitionFactory.Create(DataSourceKind.View, string.Empty, rowsType: string.Empty),
                    [],
                    [],
                    flowInput: new CardFlowInputDefinition("rows", "reporting_calendar", "localized")),
            ],
            dashflow: Flow);

        var resolved = FlowCardExecution.ApplyFlowInput(document.Cards[0], document);

        Assert.Equal("UtilizationRowLocalized", resolved.DataSource.RowsType);
        Assert.Equal("demo.v_daily_peak", resolved.DataSource.Value);
    }


    [Fact]
    public void CardDataPipeline_runs_to_zone_on_transformer_path()
    {
        var flow = Flow.WithTransformers(
        [
            Transformer(
                "reporting_calendar",
                "raw",
                "localized",
                new DashflowTransformStepDefinition(
                    BuiltinScalarTransformIds.ToZone,
                    [new DashflowTransformParameterDefinition("offset_minutes", "180")])),
        ]);

        var document = DocumentTestData.Create(
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
                    DashSpec.Execution.Parsing.DataSourceDefinitionFactory.Create(DataSourceKind.View, string.Empty, rowsType: string.Empty),
                    [],
                    [],
                    flowInput: new CardFlowInputDefinition("rows", "reporting_calendar", "localized")),
            ],
            dashflow: flow);

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
        var flow = Flow.WithSources(
        [
            Flow.Sources[0],
            Source("drill_src", "demo.v_drill", "rows", "UtilizationRow"),
        ]);

        var slots = new Dictionary<string, CardDiagramSlotDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["heatmap"] = new CardDiagramSlotDefinition
            {
                SlotRef = "heatmap",
                Diagram = new DiagramDefinition("heatmap", new Dictionary<string, string>(), null),
                DataSource = DashSpec.Execution.Parsing.DataSourceDefinitionFactory.Create(DataSourceKind.View, string.Empty),
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
                DataSource = DashSpec.Execution.Parsing.DataSourceDefinitionFactory.Create(DataSourceKind.View, string.Empty),
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
            DashSpec.Execution.Parsing.DataSourceDefinitionFactory.Create(DataSourceKind.View, string.Empty),
            [],
            [],
            diagramSlotRef: "heatmap",
            diagramSlots: slots,
            flowInput: new CardFlowInputDefinition("rows", "utilization", "utilization"));

        var document = DocumentTestData.Create(
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
            dashflow: flow);

        var materialized = DocumentFlowBinder.MaterializeCard(card, document);

        Assert.Equal("demo.v_daily_peak", materialized.DiagramSlots!["heatmap"].DataSource.Value);
        Assert.Equal("demo.v_drill", materialized.DiagramSlots!["drill"].DataSource.Value);
        Assert.Equal("demo.v_daily_peak", materialized.DataSource.Value);
    }
}
