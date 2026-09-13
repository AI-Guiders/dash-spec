#nullable enable

using AIGuiders.Platform.Authoring.Command.Catalog;

namespace DashSpec.Host.Commands;

/// <summary>
/// Federation command catalog for DashSpec Host — SSOT is <c>Catalog/dash.catalog.gdl</c> emit
/// (<c>Generated/DashCatalog.g.cs</c>, regen via MSBuild or <c>gdlc emit</c>).
/// </summary>
internal static class DashboardCatalog
{
    static readonly Lazy<CatalogDocument> Document = new(Load, isThreadSafe: true);

    public static CatalogDocument Current => Document.Value;

    public static IReadOnlyList<string> FederationSurfaces => Generated.DashCatalog.FederationSurfaces;

    public static IReadOnlyList<CatalogPhrase> Phrases => Current.Phrases;

    public static IReadOnlyList<CatalogBindingRow> Bindings => Current.Bindings;

    public static string Summary => CatalogSummary.Format(Current);

    public static CatalogPhraseSlotIndex PhraseSlots => Generated.DashCatalog.PhraseSlots;

    public const string CardViewCatalogCommand = "card.view";

    public static string CardViewWireCommandId => Generated.DashCatalog.WireCommandIds[CardViewCatalogCommand];

    public static CatalogPhraseSlotCommand CardViewPhrase =>
        PhraseSlots.Commands.First(command =>
            command.CatalogCommand.Equals(CardViewCatalogCommand, StringComparison.OrdinalIgnoreCase));

    static CatalogDocument Load()
    {
        var document = Generated.DashCatalog.Document;
        DashboardCatalogFlavor.ValidateAtLoad(document);
        return document;
    }
}
