namespace DashSpec.Abstractions.Plugins;

/// <summary>
/// Well-known host parts a plugin can supply a component for. The host renders whatever
/// component is registered here, so it never names plugin types (ADR-0077: plugins are vertical).
/// Not to be confused with layout slots (<c>LayoutSlotDescriptor</c>) — those are grid places.
/// </summary>
public static class DashSpecPluginComponents
{
    /// <summary>Card body component (supplied by a viz plugin).</summary>
    public const string CardBody = "card.body";

    /// <summary>Card interior (inner layout) component (supplied by a viz plugin).</summary>
    public const string CardInterior = "card.interior";

    /// <summary>Filter widget component (supplied by a filter plugin).</summary>
    public const string FilterWidget = "filter.widget";
}
