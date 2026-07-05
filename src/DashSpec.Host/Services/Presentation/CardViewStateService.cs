using DashSpec.Abstractions.Plugins;

namespace DashSpec.Host.Services.Presentation;

public sealed class CardViewStateService : ICardViewState
{
    private readonly Dictionary<string, string> _activeViews =
        new(StringComparer.OrdinalIgnoreCase);

    public string? GetActiveView(string cardId) =>
        _activeViews.TryGetValue(cardId, out var viewId) ? viewId : null;

    public void SetActiveView(string cardId, string viewId) =>
        _activeViews[cardId] = viewId;

    public void Clear(string cardId) => _activeViews.Remove(cardId);

    public void ClearAll() => _activeViews.Clear();
}
