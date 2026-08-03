namespace DashSpec.Core.Model;

/// <summary>Report catalog — whitelist of top-level .dashspec entry points.</summary>
public sealed record CatalogDocument(
    string Id,
    string DefaultEntryId,
    IReadOnlyList<CatalogEntryDefinition> Entries,
    IReadOnlyList<CatalogGroupDefinition>? Groups = null);

public sealed record CatalogEntryDefinition(
    string Id,
    string Title,
    string DashspecPath,
    string? GroupId = null);
