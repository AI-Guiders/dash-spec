using DashSpec.Core.Model;

namespace DashSpec.Surface.Blazor.Services.Presentation;

public sealed class CardFoldState : ICardFoldState
{
    private readonly HashSet<string> _collapsed = new(StringComparer.OrdinalIgnoreCase);

    public bool IsCollapsed(string cardId) => _collapsed.Contains(cardId);

    public void Toggle(
        string cardId,
        CardsFoldPolicy pagePolicy,
        IEnumerable<(string CardId, CardFoldMode Mode)> foldableCards)
    {
        if (_collapsed.Contains(cardId))
        {
            _collapsed.Remove(cardId);
            if (pagePolicy is CardsFoldPolicy.FocusSingle)
            {
                foreach (var (id, mode) in foldableCards)
                {
                    if (mode is CardFoldMode.None ||
                        string.Equals(id, cardId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    _collapsed.Add(id);
                }
            }

            return;
        }

        _collapsed.Add(cardId);
    }

    public void Clear() => _collapsed.Clear();
}
