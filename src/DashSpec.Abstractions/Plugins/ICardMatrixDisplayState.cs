namespace DashSpec.Abstractions.Plugins;

/// <summary>Per-card matrix label visibility overrides (session UI toggles).</summary>
public interface ICardMatrixDisplayState
{
    /// <summary>Fired when per-card matrix label toggles change (card id).</summary>
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
