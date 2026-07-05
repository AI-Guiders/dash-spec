using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

namespace DashSpec.Core.Runtime;

/// <summary>Merges <c>use</c> card preset, <c>bind dashboard</c>, diagram library, and local filters.</summary>
public static class CardResolver
{
    public static ResolvedCardView Resolve(
        CardDefinition card,
        SpecLibrary? library,
        IReadOnlyList<string> dashboardFilters)
    {
        var working = ApplyCardPreset(card, library);
        var bindSource = working.BoundFilters;
        var bound = CardBindResolver.Expand(bindSource, working.LocalFilters, dashboardFilters);
        working = working with { BoundFilters = bound };
        return CardDiagramResolver.Resolve(working, library);
    }

    public static CardDefinition ResolveCard(
        CardDefinition card,
        SpecLibrary? library,
        IReadOnlyList<string> dashboardFilters) =>
        Resolve(card, library, dashboardFilters).Card;

    public static string ResolveKind(
        CardDefinition card,
        SpecLibrary? library,
        IReadOnlyList<string> dashboardFilters) =>
        ResolveCard(card, library, dashboardFilters).Diagram.Kind;

    private static CardDefinition ApplyCardPreset(CardDefinition card, SpecLibrary? library)
    {
        if (string.IsNullOrWhiteSpace(card.UseCardPreset))
        {
            return card;
        }

        var preset = library?.TryGetCard(card.UseCardPreset)
            ?? throw new InvalidOperationException(
                $"Card '{card.Id}': card preset '{card.UseCardPreset}' was not found in @diagramlibrary.");

        var diagram = card.Diagram;
        if (NeedsDiagramPreset(diagram) && !string.IsNullOrWhiteSpace(preset.DiagramPreset))
        {
            diagram = new DiagramDefinition(string.Empty, new Dictionary<string, string>(), preset.DiagramPreset);
        }

        var dataSource = NeedsDataSourcePreset(card.DataSource)
            ? preset.DataSource ?? throw new InvalidOperationException(
                $"Card preset '{card.UseCardPreset}' has no datasource.")
            : card.DataSource;

        var boundFilters = card.BoundFilters.Count > 0 ? card.BoundFilters : preset.BindFilters;

        return card with
        {
            Diagram = diagram,
            DataSource = dataSource,
            BoundFilters = boundFilters,
        };
    }

    private static bool NeedsDiagramPreset(DiagramDefinition diagram) =>
        string.IsNullOrWhiteSpace(diagram.UsePreset) && string.IsNullOrWhiteSpace(diagram.Kind);

    private static bool NeedsDataSourcePreset(DataSourceDefinition dataSource) =>
        string.IsNullOrWhiteSpace(dataSource.Value);
}
