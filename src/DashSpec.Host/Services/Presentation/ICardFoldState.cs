using DashSpec.Core.Model;

namespace DashSpec.Host.Services.Presentation;

public interface ICardFoldState
{
    bool IsCollapsed(string cardId);

    void Toggle(string cardId, CardsFoldPolicy pagePolicy, IEnumerable<(string CardId, CardFoldMode Mode)> foldableCards);

    void Clear();
}
