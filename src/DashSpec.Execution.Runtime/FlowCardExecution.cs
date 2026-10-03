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
        var dataSource = ToDataSource(source);
        return card with { DataSource = dataSource };
    }

    public static string? ResolveProviderId(DashboardDocument document, CardDefinition card)
    {
        if (card.FlowInput is null || document.Dashflow is null)
        {
            return null;
        }

        var source = ResolveBackingSource(document.Dashflow, card.FlowInput);
        return source.ProviderInfer ? null : source.ProviderId;
    }

    private static DashflowSourceDefinition ResolveBackingSource(
        DashflowModuleDefinition flow,
        CardFlowInputDefinition input)
    {
        var source = TryResolveSource(flow, input.NodeId, input.PortName);
        if (source is null)
        {
            throw new InvalidOperationException(
                $"Card input '{input.Alias}' references '{input.NodeId}.{input.PortName}', " +
                "which does not resolve to a dashflow source output in this module.");
        }

        return source;
    }

    private static DashflowSourceDefinition? TryResolveSource(
        DashflowModuleDefinition flow,
        string nodeId,
        string portName)
    {
        var source = flow.Sources.FirstOrDefault(s =>
            string.Equals(s.Id, nodeId, StringComparison.OrdinalIgnoreCase));
        if (source is not null)
        {
            if (!PortMatches(source.OutputPort, portName))
            {
                throw new InvalidOperationException(
                    $"Source '{nodeId}' has no output port '{portName}' (output is '{source.OutputPort}').");
            }

            return source;
        }

        var transformer = flow.Transformers.FirstOrDefault(t =>
            string.Equals(t.Id, nodeId, StringComparison.OrdinalIgnoreCase));
        if (transformer is null)
        {
            return null;
        }

        if (!transformer.OutputPorts.Any(p => PortMatches(p, portName)))
        {
            throw new InvalidOperationException(
                $"Transformer '{nodeId}' has no output port '{portName}'.");
        }

        var inboundLinks = flow.Links
            .Where(l => string.Equals(l.ToNode, nodeId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (inboundLinks.Count == 0)
        {
            return null;
        }

        var inbound =
            inboundLinks.Count == 1
                ? inboundLinks[0]
                : inboundLinks.FirstOrDefault(l =>
                      !string.IsNullOrWhiteSpace(transformer.DefaultInputPort) &&
                      string.Equals(l.ToPort, transformer.DefaultInputPort, StringComparison.OrdinalIgnoreCase))
                  ?? inboundLinks[0];

        var upstreamPort = inbound.FromPort;
        if (string.IsNullOrWhiteSpace(upstreamPort))
        {
            var upstreamSource = flow.Sources.FirstOrDefault(s =>
                string.Equals(s.Id, inbound.FromNode, StringComparison.OrdinalIgnoreCase));
            upstreamPort = upstreamSource?.OutputPort;
            var upstreamTransformer = flow.Transformers.FirstOrDefault(t =>
                string.Equals(t.Id, inbound.FromNode, StringComparison.OrdinalIgnoreCase));
            upstreamPort ??= upstreamTransformer?.DefaultOutputPort;
        }

        return TryResolveSource(flow, inbound.FromNode, upstreamPort ?? string.Empty);
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
                ProviderInfer: source.ProviderInfer),
            DashflowSourceFromKind.SqlQuery => new DataSourceDefinition(
                DataSourceKind.Sql,
                source.FromValue,
                DataSourceSqlCarrier.Query,
                RowsType: source.OutputRowType,
                ProviderInfer: source.ProviderInfer),
            DashflowSourceFromKind.SqlFile => new DataSourceDefinition(
                DataSourceKind.Sql,
                source.FromValue,
                DataSourceSqlCarrier.File,
                RowsType: source.OutputRowType,
                ProviderInfer: source.ProviderInfer),
            _ => throw new InvalidOperationException($"Unsupported dashflow source carrier '{source.FromKind}'."),
        };
}
