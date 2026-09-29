using DashSpec.Core.Runtime;

namespace DashSpec.Host.Services.Presentation;

public sealed class CardCellDrillState : ICardCellDrillState
{
    private readonly Dictionary<string, CardCellDrillOverlay> _overlays = new(StringComparer.OrdinalIgnoreCase);

    public CardCellDrillOverlay? Get(string cardId) =>
        _overlays.TryGetValue(cardId, out var overlay) ? overlay : null;

    public void Set(string cardId, CardCellDrillOverlay overlay) =>
        _overlays[cardId] = overlay;

    public void Clear(string cardId) => _overlays.Remove(cardId);
}
