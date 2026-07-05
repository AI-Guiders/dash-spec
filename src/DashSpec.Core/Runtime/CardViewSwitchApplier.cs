using DashSpec.Core.Model;

namespace DashSpec.Core.Runtime;

/// <summary>Applies card <c>views { … }</c> extension block — swaps diagram preset for active view.</summary>
public static class CardViewSwitchApplier
{
    public const string ViewsBlockKeyword = "views";

    public static CardDefinition Apply(CardDefinition card, string? activeViewId)
    {
        var viewsBlock = card.ExtensionBlocks?
            .FirstOrDefault(b => string.Equals(b.Keyword, ViewsBlockKeyword, StringComparison.OrdinalIgnoreCase));
        if (viewsBlock is null || viewsBlock.Nested.Count == 0)
        {
            return card;
        }

        var viewId = ResolveViewId(viewsBlock, activeViewId);
        if (string.IsNullOrWhiteSpace(viewId))
        {
            return card;
        }

        var viewNode = viewsBlock.Nested.FirstOrDefault(v =>
            string.Equals(v.Keyword, viewId, StringComparison.OrdinalIgnoreCase));
        if (viewNode is null)
        {
            return card;
        }

        if (!viewNode.Properties.TryGetValue("diagram", out var diagramId) ||
            string.IsNullOrWhiteSpace(diagramId))
        {
            return card;
        }

        return card with
        {
            Diagram = new DiagramDefinition(string.Empty, new Dictionary<string, string>(), diagramId),
        };
    }

    public static string? ResolveDefaultViewId(IReadOnlyList<ExtensionBlockNode> extensionBlocks)
    {
        var viewsBlock = extensionBlocks
            .FirstOrDefault(b => string.Equals(b.Keyword, ViewsBlockKeyword, StringComparison.OrdinalIgnoreCase));
        if (viewsBlock is null)
        {
            return null;
        }

        if (viewsBlock.Properties.TryGetValue("default", out var defaultId) &&
            !string.IsNullOrWhiteSpace(defaultId))
        {
            return defaultId;
        }

        return viewsBlock.Nested.Count > 0 ? viewsBlock.Nested[0].Keyword : null;
    }

    private static string? ResolveViewId(ExtensionBlockNode viewsBlock, string? activeViewId)
    {
        if (!string.IsNullOrWhiteSpace(activeViewId))
        {
            return activeViewId;
        }

        if (viewsBlock.Properties.TryGetValue("default", out var defaultId) &&
            !string.IsNullOrWhiteSpace(defaultId))
        {
            return defaultId;
        }

        return viewsBlock.Nested.Count > 0 ? viewsBlock.Nested[0].Keyword : null;
    }
}
