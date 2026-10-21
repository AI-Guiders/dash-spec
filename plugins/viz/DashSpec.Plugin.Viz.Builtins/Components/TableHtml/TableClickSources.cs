namespace DashSpec.Plugin.Viz.Builtins.Components.TableHtml;

/// <summary>
/// Table click sources (ADR-0107): the source vocabulary this plugin renders.
/// The parse-time registry (KnownClickSources) is fed from these words.
/// </summary>
public static class TableClickSources
{
    /// <summary>Body row click (the first column's value rides YLabel).</summary>
    public const string Row = "row";
}
