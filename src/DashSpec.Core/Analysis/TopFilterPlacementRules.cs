using DashSpec.Core.Parsing;

namespace DashSpec.Core.Analysis;

internal static class TopFilterPlacementRules
{
    public static string? GetViolation(
        string filterName,
        string cardId,
        string diagramKind,
        bool hasUnresolvedPreset)
    {
        if (hasUnresolvedPreset)
        {
            return null;
        }

        if (DiagramKindRegistry.SupportsTopLimit(diagramKind))
        {
            return null;
        }

        var kindLabel = string.IsNullOrWhiteSpace(diagramKind) ? "(preset)" : diagramKind;
        return $"Top filter '{filterName}' can only be placed on table cards; card '{cardId}' uses diagram {kindLabel}.";
    }
}
