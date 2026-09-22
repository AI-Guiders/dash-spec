using AIGuiders.Platform.Modeling.Language;
using AIGuiders.Surface.Wpf.Abstractions;
using AIGuiders.Surface.Wpf.CodeCenter;
using DashSpec.Modeling.CodeCenter;
using DashSpec.Modeling.Language.Adapters.DashSpec;

namespace DashSpec.CodeCenter.Plugin;

/// <summary>DashSpec language family (language.dashspec).</summary>
public sealed class DashSpecLanguageFamily : AIGuiders.Platform.Execution.Language.ILanguageFamilyPlugin
{
    public string PluginId => "language.dashspec";

    public string LanguageId => LanguageIds.Dashspec;

    public string DocumentProfileId => "dashspec.block";

    public IReadOnlyList<string> FileExtensions => DashSpecPathRules.extensions;

    public bool MatchesDocument(string documentPathOrId) =>
        DashSpecPathRules.isDashSpecPath(documentPathOrId)
        || documentPathOrId.StartsWith("doc://dash", StringComparison.OrdinalIgnoreCase);

    public ILanguageBackend CreateLanguageBackend() => new DashSpecLanguageBackend();
}

/// <summary>Code Center document open for DashSpec (Forge vertical-slice hook on meta-plugin).</summary>
public sealed class DashSpecCodeCenterLanguageBackend : ICodeCenterLanguageBackend
{
    static readonly DashSpecLanguageFamily Family = new();

    public string LanguageId => Family.LanguageId;

    public string ProfileId => Family.DocumentProfileId;

    public IReadOnlyList<string> FileExtensions => Family.FileExtensions;

    public bool IsFallback => false;

    public bool MatchesDocument(string documentPathOrId) => Family.MatchesDocument(documentPathOrId);

    public IDocumentSession CreateSession(string documentId, string text) =>
        new FederationCodeCenterSession(
            DashSpecCodeCenterSession.createDocumentSession(documentId, text));
}
