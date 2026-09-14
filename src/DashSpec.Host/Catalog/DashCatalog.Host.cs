#nullable enable

using AIGuiders.Platform.Authoring.Command.Catalog;
using DashSpec.Host.Commands;

namespace DashSpec.Generated;

/// <summary>Host accessors over emit SSOT (<c>dash.catalog.gdl</c> → <c>DashCatalog.g.cs</c>, ADR-0051 I2).</summary>
public static partial class DashCatalog
{
    public const string CardViewCatalogCommand = "card.view";

    public static IReadOnlyList<CatalogPhrase> Phrases => Document.Phrases;

    public static IReadOnlyList<CatalogBindingRow> Bindings => Document.Bindings;

    public static string Summary => CatalogSummary.Format(Document);

    public static string CardViewWireCommandId => WireCommandIds[CardViewCatalogCommand];

    public static CatalogPhraseSlotCommand CardViewPhrase =>
        PhraseSlots.Commands.First(command =>
            command.CatalogCommand.Equals(CardViewCatalogCommand, StringComparison.OrdinalIgnoreCase));

    static DashCatalog()
    {
        DashboardCatalogFlavor.ValidateAtLoad(Document);
    }
}
