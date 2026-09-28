using DashSpec.Core.Model;

namespace DashSpec.Viz;

public interface IOnClickInteractionService
{
    ShowSelectionEffect? ResolveShowEffect(CardClickBehaviour? behaviour);

    bool HasNavigationEffects(CardClickBehaviour? behaviour);

    IEnumerable<CardClickEffect> ExpandClickEffects(IEnumerable<CardClickEffect> effects);
}
