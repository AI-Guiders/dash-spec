using DashSpec.Core.Model;

namespace DashSpec.Execution.Runtime;

/// <summary>Resolve dashflow path from card input back to source (ADR-0078).</summary>
public static class DashflowPathResolver
{
    public static DashflowExecutionPath Resolve(DashflowModuleDefinition flow, CardFlowInputDefinition input)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(input);

        var transformers = new List<DashflowTransformerDefinition>();
        var nodeId = input.NodeId;
        var portName = input.PortName;

        while (true)
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

                return new DashflowExecutionPath(source, transformers);
            }

            var transformer = flow.Transformers.FirstOrDefault(t =>
                string.Equals(t.Id, nodeId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    $"Card input '{input.Alias}' references unknown dashflow node '{nodeId}'.");

            if (!transformer.Outputs.Any(p => PortMatches(p.Name, portName)))
            {
                throw new InvalidOperationException(
                    $"Transformer '{nodeId}' has no output port '{portName}'.");
            }

            transformers.Insert(0, transformer);

            var inboundLinks = flow.Links
                .Where(l => string.Equals(l.ToNode, nodeId, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (inboundLinks.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Transformer '{nodeId}' has no inbound links in dashflow '{flow.FlowId}'.");
            }

            var inbound = inboundLinks.Count == 1
                ? inboundLinks[0]
                : inboundLinks.FirstOrDefault(l =>
                      !string.IsNullOrWhiteSpace(transformer.DefaultInputPort)
                      && string.Equals(l.ToPort, transformer.DefaultInputPort, StringComparison.OrdinalIgnoreCase))
                  ?? inboundLinks[0];

            nodeId = inbound.FromNode;
            portName = ResolveUpstreamPort(flow, inbound);
        }
    }

    private static string ResolveUpstreamPort(DashflowModuleDefinition flow, DashflowLinkDefinition inbound)
    {
        if (!string.IsNullOrWhiteSpace(inbound.FromPort))
        {
            return inbound.FromPort;
        }

        var upstreamSource = flow.Sources.FirstOrDefault(s =>
            string.Equals(s.Id, inbound.FromNode, StringComparison.OrdinalIgnoreCase));
        if (upstreamSource is not null)
        {
            return upstreamSource.OutputPort;
        }

        var upstreamTransformer = flow.Transformers.FirstOrDefault(t =>
            string.Equals(t.Id, inbound.FromNode, StringComparison.OrdinalIgnoreCase));
        if (upstreamTransformer is not null && !string.IsNullOrWhiteSpace(upstreamTransformer.DefaultOutputPort))
        {
            return upstreamTransformer.DefaultOutputPort;
        }

        return string.Empty;
    }

    private static bool PortMatches(string expected, string actual) =>
        string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
}
