#nullable enable

using AIGuiders.Platform.Authoring.Command.Bundles;
using AIGuiders.Platform.Authoring.Command.Catalog;
using AIGuiders.Platform.Authoring.Core;

namespace DashSpec.Host.Commands;

/// <summary>
/// Loads <c>Catalog/dash.catalog.gdl</c> — federation SSOT for surfaces and notation contract.
/// <para>
/// <b>Generated</b> (<c>Generated/DashCatalog.g.cs</c>, regen via MSBuild or
/// <c>gdlc emit --project authoring/dashspec.gdlproj</c>): <c>FederationSurfaces</c>,
/// <c>PhraseSlots</c>, wire command ids, MCP expose list.
/// </para>
/// <para>
/// <b>Runtime parse</b> (this loader): phrases, bindings, channels, defaults (flavor, chord-root),
/// commands/profiles — needed for CommandPlane expansion and flavor validation.
/// </para>
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
