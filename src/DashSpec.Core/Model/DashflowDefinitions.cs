namespace DashSpec.Core.Model;

public sealed record DashflowModuleDefinition(
    string FlowId,
    IReadOnlyList<DashflowSourceDefinition> Sources,
    IReadOnlyList<DashflowTransformerDefinition> Transformers,
    IReadOnlyList<DashflowLinkDefinition> Links);

public sealed record DashflowSourceDefinition(
    string Id,
    bool ProviderInfer,
    string? ProviderId,
    DashflowSourceFromKind FromKind,
    string FromValue,
    string OutputPort,
    string OutputRowType);

public enum DashflowSourceFromKind
{
    View,
    SqlQuery,
    SqlFile,
}

public sealed record DashflowTransformerPortDefinition(string Name, string RowType);

public sealed record DashflowTransformParameterDefinition(string Key, string Value);

public sealed record DashflowTransformStepDefinition(
    string PluginId,
    IReadOnlyList<DashflowTransformParameterDefinition> Parameters);

public sealed record DashflowTransformerDefinition(
    string Id,
    IReadOnlyList<DashflowTransformerPortDefinition> Outputs,
    string? DefaultInputPort,
    string? DefaultOutputPort,
    IReadOnlyList<DashflowTransformStepDefinition> Steps);

public sealed record DashflowLinkDefinition(
    string FromNode,
    string? FromPort,
    string ToNode,
    string? ToPort);

public sealed record CardFlowInputDefinition(
    string Alias,
    string NodeId,
    string PortName);
