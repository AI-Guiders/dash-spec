namespace DashSpec.Core.Model;

/// <summary>Resolved SQL source plus transformers from source to a card flow input (ADR-0078).</summary>
public sealed record DashflowExecutionPath(
    DashflowSourceDefinition Source,
    IReadOnlyList<DashflowTransformerDefinition> Transformers);
