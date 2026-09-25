namespace DashSpec.Core.Model;

public sealed record HostDocument(
    string Id,
    string CatalogPath,
    IReadOnlyDictionary<string, string> Configuration,
    IReadOnlyDictionary<string, string> Presentation,
    IReadOnlyList<HostLinkDefinition> Links,
    IReadOnlyList<string> Surfaces,
    LayoutBoardDefinition? TopbarLayout);
