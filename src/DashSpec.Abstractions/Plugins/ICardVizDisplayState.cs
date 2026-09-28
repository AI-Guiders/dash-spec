namespace DashSpec.Abstractions.Plugins;

/// <summary>Per-card viz display overrides (axis/value labels); diagram-agnostic session state (ADR-0062).</summary>
public interface ICardVizDisplayState
{
    event Action<string>? Changed;

    bool? GetValueLabelsOverride(string cardId);

    bool? GetAxisLabelsXOverride(string cardId);

    bool? GetAxisLabelsYOverride(string cardId);

    void SetValueLabelsOverride(string cardId, bool? visible);

    void SetAxisLabelsXOverride(string cardId, bool? visible);

    void SetAxisLabelsYOverride(string cardId, bool? visible);

    void ToggleValueLabels(string cardId, bool currentlyVisible);

    void ToggleAxisLabelsX(string cardId, bool currentlyVisible);

    void ToggleAxisLabelsY(string cardId, bool currentlyVisible);

    void ClearAll();
}
