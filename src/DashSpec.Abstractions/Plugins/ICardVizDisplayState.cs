namespace DashSpec.Abstractions.Plugins;

/// <summary>Per-card viz display overrides (axis/value labels); diagram-agnostic session state (ADR-0062).</summary>
public interface ICardVizDisplayState
{
    event Action<string>? Changed;

    bool? GetValueLabelsOverride(string cardId);

    bool? GetAxisLabelsXOverride(string cardId);

    bool? GetAxisLabelsYOverride(string cardId);

    bool? GetLegendOverride(string cardId);

    void SetValueLabelsOverride(string cardId, bool? visible);

    void SetAxisLabelsXOverride(string cardId, bool? visible);

    void SetAxisLabelsYOverride(string cardId, bool? visible);

    void SetLegendOverride(string cardId, bool? visible);

    void ToggleValueLabels(string cardId, bool currentlyVisible);

    void ToggleAxisLabelsX(string cardId, bool currentlyVisible);

    void ToggleAxisLabelsY(string cardId, bool currentlyVisible);

    void ToggleLegend(string cardId, bool currentlyVisible);

    /// <summary>Interactive grouping (server-side, like WHERE): the column the table card is
    /// currently grouped by — the re-render compiles it into the slot's SQL (GROUP BY),
    /// the way bound filters ride the same request. Null = ungrouped.</summary>
    string? GetGroupByOverride(string cardId);

    void SetGroupByOverride(string cardId, string? column);

    void ClearAll();
}
