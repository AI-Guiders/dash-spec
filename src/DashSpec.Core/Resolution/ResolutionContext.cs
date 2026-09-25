using DashSpec.Core.Model;

namespace DashSpec.Core.Resolution;

/// <summary>Inputs for <see cref="DisplayResolution"/> (ADR-0057).</summary>
public sealed record ResolutionContext(
    ResolutionMode Mode,
    DashboardDocument Document,
    CatalogEntryDefinition? CatalogEntry = null,
    TabDefinition? Tab = null)
{
    public static ResolutionContext ForCatalog(
        DashboardDocument document,
        CatalogEntryDefinition entry,
        TabDefinition? tab = null) =>
        new(ResolutionMode.CatalogProd, document, entry, tab);

    public static ResolutionContext ForSoak(DashboardDocument document, TabDefinition? tab = null) =>
        new(ResolutionMode.SoakDev, document, null, tab);

    public static ResolutionContext ForEmbed(
        DashboardDocument document,
        CatalogEntryDefinition? entry = null,
        TabDefinition? tab = null) =>
        new(ResolutionMode.DashboardEmbed, document, entry, tab);
}
