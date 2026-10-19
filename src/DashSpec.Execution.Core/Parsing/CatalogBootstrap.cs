using DashSpec.Core.Parsing;

namespace DashSpec.Execution.Parsing;

public sealed record CatalogBootstrap(CatalogDocument Document, string FullPath)
{
    public CatalogEntryDefinition RequireEntry(string entryId) =>
        Document.Entries.FirstOrDefault(e => string.Equals(e.Id, entryId, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Catalog entry '{entryId}' not found.");

    public string ResolveEntrySpecFullPath(string entryId) =>
        CatalogReader.ResolveEntrySpecPath(FullPath, RequireEntry(entryId).DashspecPath);
}
