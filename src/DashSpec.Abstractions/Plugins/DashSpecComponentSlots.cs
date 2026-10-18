namespace DashSpec.Abstractions.Plugins;

/// <summary>
/// Well-known component slots: the host renders whatever component a plugin registers here,
/// so the host never names plugin types (ADR-0077: plugins are vertical).
/// </summary>
public static class DashSpecComponentSlots
{
    /// <summary>Card body host component (supplied by a viz plugin).</summary>
    public const string CardHost = "card.host";

    /// <summary>Card interior-slot host component (supplied by a viz plugin).</summary>
    public const string CardInteriorHost = "card.interiorHost";

    /// <summary>Filter widget host component (supplied by a filter plugin).</summary>
    public const string FilterWidgetHost = "filter.widgetHost";
}
