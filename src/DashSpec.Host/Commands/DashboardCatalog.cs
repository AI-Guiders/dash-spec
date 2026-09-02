#nullable enable

using AIGuiders.Platform.Authoring.Command.Bundles;
using AIGuiders.Platform.Authoring.Command.Catalog;
using AIGuiders.Platform.Authoring.Core;

namespace DashSpec.Host.Commands;

/// <summary>Loads <c>Catalog/dash.catalog.gdl</c> — federation SSOT for surfaces and notation contract.</summary>
internal static class DashboardCatalog
{
    static readonly Lazy<CatalogDocument> Document = new(Load, isThreadSafe: true);

    public static CatalogDocument Current => Document.Value;

    public static IReadOnlyList<string> FederationSurfaces => Current.FederationSurfaces();

    public static IReadOnlyList<CatalogPhrase> Phrases => Current.Phrases;

    public static IReadOnlyList<CatalogBindingRow> Bindings => Current.Bindings;

    public static string Summary => CatalogSummary.Format(Current);

    public static CatalogPhraseSlotIndex PhraseSlots => PhraseSlotIndex.Value;

    static readonly Lazy<CatalogPhraseSlotIndex> PhraseSlotIndex = new(
        () => CatalogPhraseSlotIndex.FromDocument(Current),
        isThreadSafe: true);

    static CatalogDocument Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Catalog", "dash.catalog.gdl");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Missing dash.catalog.gdl SSOT.", path);
        }

        var result = CatalogParser.ParseFile(path, CatalogBundleLibrary.Federation);
        if (result.Document is null)
        {
            var message = string.Join("; ", result.Diagnostics.Select(static d => d.Message));
            throw new InvalidOperationException($"dash.catalog.gdl parse failed: {message}");
        }

        var errors = result.Diagnostics.Where(static d =>
            d.Code is AuthoringDiagnosticCode.GrammarWireMismatch
                or AuthoringDiagnosticCode.MissingGrammarDeclaration
                or AuthoringDiagnosticCode.MissingCatalogHeader
                or AuthoringDiagnosticCode.UnknownGrammarId
                or AuthoringDiagnosticCode.UnknownBundle
                or AuthoringDiagnosticCode.UnknownProfile).ToList();

        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"dash.catalog.gdl validation failed: {string.Join("; ", errors.Select(static e => e.Message))}");
        }

        DashboardCatalogFlavor.ValidateAtLoad(result.Document);
        return result.Document;
    }
}
