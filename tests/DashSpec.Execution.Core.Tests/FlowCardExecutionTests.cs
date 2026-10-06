using DashSpec.Core.Model;
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
            new DashflowLinkDefinition("utilization", "utilization", "reporting_calendar", "raw"),
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
            FiltersChromeDefinition.Default,
            [],
            [],
            [],
            [
                new CardDefinition(
                    "c",
                    "C",
                    new DiagramDefinition("table", new Dictionary<string, string>()),
                    new DataSourceDefinition(DataSourceKind.View, string.Empty, RowsType: string.Empty),
                    [],
                    [],
                    FlowInput: new CardFlowInputDefinition("rows", "utilization", "utilization")),
            ],
            Dashflow: Flow);

        var resolved = FlowCardExecution.ApplyFlowInput(document.Cards[0], document);

        Assert.Equal("demo.v_daily_peak", resolved.DataSource.Value);
        Assert.Equal("UtilizationRow", resolved.DataSource.RowsType);
    }

    [Fact]
    public void ResolveProviderId_returns_named_source_provider()
    {
        var card = new CardDefinition(
            "c",
            "C",
            new DiagramDefinition("table", new Dictionary<string, string>()),
            new DataSourceDefinition(DataSourceKind.View, string.Empty),
            [],
            [],
            FlowInput: new CardFlowInputDefinition("rows", "utilization", "utilization"));

        var document = new DashboardDocument(
            "t",
            "T",
            null,
            SqlDialect.TSql,
            null,
            null,
            null,
            LayoutDefinition.Default,
            FiltersChromeDefinition.Default,
            [],
            [],
            [],
            [card],
            Dashflow: Flow);

        Assert.Equal("postgres", FlowCardExecution.ResolveProviderId(document, card));
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
            FiltersChromeDefinition.Default,
            [],
            [],
            [],
            [
                new CardDefinition(
                    "c",
                    "C",
                    new DiagramDefinition("table", new Dictionary<string, string>()),
                    new DataSourceDefinition(DataSourceKind.View, string.Empty, RowsType: string.Empty),
                    [],
                    [],
                    FlowInput: new CardFlowInputDefinition("rows", "reporting_calendar", "localized")),
            ],
            Dashflow: Flow);

        var resolved = FlowCardExecution.ApplyFlowInput(document.Cards[0], document);

        Assert.Equal("UtilizationRowLocalized", resolved.DataSource.RowsType);
        Assert.Equal("demo.v_daily_peak", resolved.DataSource.Value);
    }
}
