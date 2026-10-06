using System.Collections.Generic;
using DashSpec.Core.Layout;
using DashSpec.Core.Model;

namespace DashSpec.Execution.Runtime;

/// <summary>
/// Legacy runtime materialize for documents parsed without <see cref="DashSpec.Modeling.Parse.Document.DocumentFlowMaterializer"/>.
/// Parse pipeline materializes in Modeling; this path is idempotent when datasources are already resolved.
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

        if (document.Dashflow is null)
        {
            return card;
        }

        var result = card;
        if (result.FlowInput is not null && string.IsNullOrWhiteSpace(result.DataSource.Value))
        {
            result = FlowCardExecution.ApplyFlowInput(result, document);
        }

        if (result.DiagramSlots is not { Count: > 0 })
        {
            return result;
        }

        var slots = new Dictionary<string, CardDiagramSlotDefinition>(StringComparer.OrdinalIgnoreCase);
        var changed = false;
        foreach (var (slotRef, slot) in result.DiagramSlots)
        {
            var materialized = FlowCardExecution.ApplyFlowInputToSlot(slot, document);
            if (!ReferenceEquals(materialized, slot))
            {
                changed = true;
            }

            slots[slotRef] = materialized;
        }

        if (!changed)
        {
            return result;
        }

        var primaryRef = CardDiagramSlotCatalog.ResolvePrimarySlotRef(result);
        if (slots.TryGetValue(primaryRef, out var primary) &&
            string.IsNullOrWhiteSpace(result.DataSource.Value) &&
            !string.IsNullOrWhiteSpace(primary.DataSource.Value))
        {
            result = result with { DataSource = primary.DataSource };
        }

        return result with { DiagramSlots = slots };
    }
}
