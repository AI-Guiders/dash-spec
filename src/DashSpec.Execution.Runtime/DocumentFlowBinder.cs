using DashSpec.Core.Model;

namespace DashSpec.Execution.Runtime;

/// <summary>
/// Single bind point: card <c>input from node.port</c> → concrete <see cref="DataSourceDefinition"/> (ADR-0078 P3b).
/// </summary>
public static class DocumentFlowBinder
{
    public static DashboardDocument MaterializeFlowCards(DashboardDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Dashflow is null || document.Cards.Count == 0)
        {
            return document;
        }

        var changed = false;
        var cards = new List<CardDefinition>(document.Cards.Count);
        foreach (var card in document.Cards)
        {
            var materialized = MaterializeCard(card, document);
            if (!ReferenceEquals(materialized, card))
            {
                changed = true;
            }

            cards.Add(materialized);
        }

        return changed ? document with { Cards = cards } : document;
    }

    public static CardDefinition MaterializeCard(CardDefinition card, DashboardDocument document)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(document);

        if (card.FlowInput is null || document.Dashflow is null)
        {
            return card;
        }

        if (!string.IsNullOrWhiteSpace(card.DataSource.Value))
        {
            return card;
        }

        return FlowCardExecution.ApplyFlowInput(card, document);
    }
}
