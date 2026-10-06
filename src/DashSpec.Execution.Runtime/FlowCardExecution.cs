using DashSpec.Core.Model;

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

        var source = ResolveBackingSource(document.Dashflow, card.FlowInput);
        var rowType = ResolvePortRowType(document.Dashflow, card.FlowInput.NodeId, card.FlowInput.PortName)
            ?? source.OutputRowType;
        var dataSource = ToDataSource(source) with { RowsType = rowType };
        return card with { DataSource = dataSource };
    }

    public static string? ResolveProviderId(CardDefinition card)
    {
        if (card.FlowInput is not null && string.IsNullOrWhiteSpace(card.DataSource.Value))
        {
            throw new InvalidOperationException(
                $"Card '{card.Id}' has flow input but no materialized datasource; call DocumentFlowBinder.MaterializeCard first.");
        }

        return card.DataSource.ProviderInfer ? null : card.DataSource.ProviderId;
    }

    public static DashflowExecutionPath ResolvePath(DashboardDocument document, CardFlowInputDefinition input)
    {
        if (document.Dashflow is null)
        {
            throw new InvalidOperationException("Dashboard document has no resolved dashflow module.");
        }

        return DashflowPathResolver.Resolve(document.Dashflow, input);
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
            .FirstOrDefault(p => PortMatches(p.Name, portName))
            ?.RowType;
    }

    private static bool PortMatches(string expected, string actual) =>
        string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);

    private static DataSourceDefinition ToDataSource(DashflowSourceDefinition source) =>
        source.FromKind switch
        {
            DashflowSourceFromKind.View => new DataSourceDefinition(
                DataSourceKind.View,
                source.FromValue,
                RowsType: source.OutputRowType,
                ProviderInfer: source.ProviderInfer,
                ProviderId: source.ProviderInfer ? null : source.ProviderId),
            DashflowSourceFromKind.SqlQuery => new DataSourceDefinition(
                DataSourceKind.Sql,
                source.FromValue,
                DataSourceSqlCarrier.Query,
                RowsType: source.OutputRowType,
                ProviderInfer: source.ProviderInfer,
                ProviderId: source.ProviderInfer ? null : source.ProviderId),
            DashflowSourceFromKind.SqlFile => new DataSourceDefinition(
                DataSourceKind.Sql,
                source.FromValue,
                DataSourceSqlCarrier.File,
                RowsType: source.OutputRowType,
                ProviderInfer: source.ProviderInfer,
                ProviderId: source.ProviderInfer ? null : source.ProviderId),
            _ => throw new InvalidOperationException($"Unsupported dashflow source carrier '{source.FromKind}'."),
        };
}
