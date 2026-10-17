using DashSpec.Core.Model;

namespace DashSpec.Viz;

public interface IOnClickInteractionService
{
    ShowSelectionEffect? ResolveShowEffect(DashSpec.Modeling.Parse.Card.CardClickBehaviour? behaviour);

    bool HasNavigationEffects(DashSpec.Modeling.Parse.Card.CardClickBehaviour? behaviour);

    IEnumerable<CardClickEffect> ExpandClickEffects(
        IEnumerable<DashSpec.Modeling.Parse.Card.CardClickEffect> effects);
}
