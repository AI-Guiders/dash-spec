namespace DashSpec.Abstractions.Viz;

/// <summary>
/// Table click sources (ADR-0107): the source vocabulary shared by every table renderer
/// (builtin HTML table, Radzen grid). The parse-time registry (KnownClickSources) is fed
/// from these words — renderers must not invent private vocabularies.
/// </summary>
public static class TableClickSources
{
    /// <summary>Body row click (the first column's value rides YLabel).</summary>
    public const string Row = "row";
}
