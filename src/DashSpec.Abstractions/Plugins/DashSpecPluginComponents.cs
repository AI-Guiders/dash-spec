namespace DashSpec.Abstractions.Plugins;

/// <summary>
/// Well-known host parts a plugin can supply a component for. The host renders whatever
/// component is registered here, so it never names plugin types (ADR-0077: plugins are vertical).
/// Not to be confused with layout slots (<c>LayoutSlotDescriptor</c>) — those are grid places.
/// </summary>
public static class DashSpecPluginComponents
{
    /// <summary>Diagram component (supplied by a viz plugin) — used for any diagram, primary or from an inner slot.</summary>
    public const string CardDiagram = "card.diagram";

    /// <summary>Filter widget component (supplied by a filter plugin).</summary>
    public const string FilterWidget = "filter.widget";
}
