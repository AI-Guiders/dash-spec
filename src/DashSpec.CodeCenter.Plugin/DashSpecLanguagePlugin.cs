using AIGuiders.Platform.Modeling.Language;
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
