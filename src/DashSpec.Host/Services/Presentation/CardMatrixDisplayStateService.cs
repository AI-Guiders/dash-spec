using DashSpec.Abstractions.Plugins;

namespace DashSpec.Host.Services.Presentation;

public sealed class CardMatrixDisplayStateService : ICardMatrixDisplayState
{
    private readonly Dictionary<string, bool?> _valueLabels =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, bool?> _axisLabelsX =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, bool?> _axisLabelsY =
        new(StringComparer.OrdinalIgnoreCase);

    public bool? GetValueLabelsOverride(string cardId) =>
        TryGet(_valueLabels, cardId);

    public bool? GetAxisLabelsXOverride(string cardId) =>
        TryGet(_axisLabelsX, cardId);

    public bool? GetAxisLabelsYOverride(string cardId) =>
        TryGet(_axisLabelsY, cardId);

    public void SetValueLabelsOverride(string cardId, bool? visible) =>
        Set(_valueLabels, cardId, visible);

    public void SetAxisLabelsXOverride(string cardId, bool? visible) =>
        Set(_axisLabelsX, cardId, visible);

    public void SetAxisLabelsYOverride(string cardId, bool? visible) =>
        Set(_axisLabelsY, cardId, visible);

    public void ToggleValueLabels(string cardId, bool currentlyVisible) =>
        SetValueLabelsOverride(cardId, !currentlyVisible);

    public void ToggleAxisLabelsX(string cardId, bool currentlyVisible) =>
        SetAxisLabelsXOverride(cardId, !currentlyVisible);

    public void ToggleAxisLabelsY(string cardId, bool currentlyVisible) =>
        SetAxisLabelsYOverride(cardId, !currentlyVisible);

    public void ClearAll()
    {
        _valueLabels.Clear();
        _axisLabelsX.Clear();
        _axisLabelsY.Clear();
    }

    private static bool? TryGet(Dictionary<string, bool?> store, string cardId) =>
        store.TryGetValue(cardId, out var value) ? value : null;

    private static void Set(Dictionary<string, bool?> store, string cardId, bool? visible)
    {
        if (visible is null)
        {
            store.Remove(cardId);
            return;
        }

        store[cardId] = visible;
    }
}
