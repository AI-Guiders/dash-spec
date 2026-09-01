#nullable enable

using AIGuiders.Platform.Authoring.Command.Bundles;
using AIGuiders.Platform.Authoring.Command.Catalog;
using AIGuiders.Platform.Authoring.Core;

namespace DashSpec.Host.Commands;

/// <summary>Loads <c>Catalog/dash.catalog</c> — federation SSOT for surfaces and notation contract.</summary>
internal static class DashboardCatalog
{
    static readonly Lazy<CatalogDocument> Document = new(Load, isThreadSafe: true);

    public static CatalogDocument Current => Document.Value;

    public static IReadOnlyList<string> FederationSurfaces => Current.FederationSurfaces();

    public static string Summary => CatalogSummary.Format(Current);

    static CatalogDocument Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Catalog", "dash.catalog");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Missing dash.catalog SSOT.", path);
        }

        var result = CatalogParser.ParseFile(path, CatalogBundleLibrary.Federation);
        if (result.Document is null)
        {
            var message = string.Join("; ", result.Diagnostics.Select(static d => d.Message));
            throw new InvalidOperationException($"dash.catalog parse failed: {message}");
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
            throw new InvalidOperationException($"dash.catalog validation failed: {string.Join("; ", errors.Select(static e => e.Message))}");
        }

        return result.Document;
    }
}
