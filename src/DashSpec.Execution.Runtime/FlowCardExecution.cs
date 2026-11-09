using Microsoft.FSharp.Core;
using FSharpDataFlow = DashSpec.Modeling.Parse.DataFlow;

namespace DashSpec.Execution.Runtime;

/// <summary>Resolves card <c>input</c> against module dashflow for query compile (ADR-0078 P3b).</summary>
public static class FlowCardExecution
{
    public static CardDefinition ApplyFlowInput(CardDefinition card, DashboardDocument document)
    {
        if (card.FlowInput is null || document.Dashflow is null)
        {
            return card;
        }

        if (!string.IsNullOrWhiteSpace(card.DataSource.Value))
        {
            return card;
        }

        var dataSource = MaterializeDataSource(document.Dashflow!.Value, card.FlowInput.Value);
        return card.WithDataSource(dataSource);
    }

    public static CardDiagramSlotDefinition ApplyFlowInputToSlot(
        CardDiagramSlotDefinition slot,
        DashboardDocument document)
    {
        if (slot.FlowInput is null || document.Dashflow is null)
        {
            return slot;
        }

        if (!string.IsNullOrWhiteSpace(slot.DataSource.Value))
        {
            return slot;
        }

        var dataSource = MaterializeDataSource(document.Dashflow!.Value, slot.FlowInput.Value);
        return slot.WithDataSource(dataSource);
    }

    public static DataSourceDefinition MaterializeDataSource(
        DashflowModuleDefinition flow,
        CardFlowInputDefinition input)
    {
        var source = ResolveBackingSource(flow, input);
        var rowType = ResolvePortRowType(flow, input.NodeId, input.PortName)
            ?? source.OutputRowType;
        return ToDataSource(source).WithRowsType(rowType);
    }

    public static string? ResolveProviderId(CardDefinition card)
    {
        if (card.FlowInput is not null && string.IsNullOrWhiteSpace(card.DataSource.Value))
        {
            throw new InvalidOperationException(
                $"Card '{card.Id}' has flow input but no materialized datasource; call DocumentFlowBinder.MaterializeCard first.");
        }

        return card.DataSource.ProviderInfer ? null : card.DataSource.ProviderId?.Value;
    }

    public static DashflowExecutionPath ResolvePath(DashboardDocument document, CardFlowInputDefinition input)
    {
        if (document.Dashflow is null)
        {
            throw new InvalidOperationException("Dashboard document has no resolved dashflow module.");
        }

        return DashflowPathResolver.Resolve(document.Dashflow!.Value, input);
    }

    private static DashflowSourceDefinition ResolveBackingSource(
        DashflowModuleDefinition flow,
        CardFlowInputDefinition input) =>
        DashflowPathResolver.Resolve(flow, input).Source;

    private static string? ResolvePortRowType(
        DashflowModuleDefinition flow,
        string nodeId,
        string portName)
    {
        var source = flow.Sources.FirstOrDefault(s =>
            string.Equals(s.Id, nodeId, StringComparison.OrdinalIgnoreCase));
        if (source is not null)
        {
            return PortMatches(source.OutputPort, portName) ? source.OutputRowType : null;
        }

        var transformer = flow.Transformers.FirstOrDefault(t =>
            string.Equals(t.Id, nodeId, StringComparison.OrdinalIgnoreCase));
        if (transformer is null)
        {
            return null;
        }

        return transformer.Outputs
            .FirstOrDefault(p => PortMatches(p.Item1, portName))
            ?.Item2;
    }

    private static bool PortMatches(string expected, string actual) =>
        string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);

    private static DataSourceDefinition ToDataSource(DashflowSourceDefinition source)
    {
        if (source.Origin is not FSharpDataFlow.SourceOrigin.Leaf leaf)
        {
            // ADR-0126: a composite source (InnerJoin/UnionAll/…) cannot
            // materialize into a legacy card datasource yet.
            throw new InvalidOperationException(
                $"Dashflow source '{source.Id}' has a composite origin (ADR-0126): composite sources cannot materialize into a legacy card datasource yet.");
        }

        var kind = leaf.kind.Equals(DashflowSourceFromKind.View)
            ? DataSourceKind.View
            : DataSourceKind.Sql;
        DataSourceSqlCarrier? carrier = leaf.kind switch
        {
            { } carrierKind when carrierKind.Equals(DashflowSourceFromKind.View) => null,
            { } carrierKind when carrierKind.Equals(DashflowSourceFromKind.SqlQuery) => DataSourceSqlCarrier.Query,
            { } carrierKind when carrierKind.Equals(DashflowSourceFromKind.SqlFile) => DataSourceSqlCarrier.File,
            _ => throw new InvalidOperationException($"Unsupported dashflow source carrier '{leaf.kind}'."),
        };

        return new DataSourceDefinition
        {
            Kind = kind,
            Value = leaf.value,
            SqlCarrier = carrier is { } value
                ? FSharpOption<DataSourceSqlCarrier>.Some(value)
                : FSharpOption<DataSourceSqlCarrier>.None,
            Sheet = FSharpOption<string>.None,
            RowsType = source.OutputRowType,
            ProviderInfer = FSharpDataFlow.DashflowProviderBridge.isInfer(source.Provider),
            ProviderId = FSharpDataFlow.DashflowProviderBridge.tryNamedId(source.Provider),
        };
    }
}
