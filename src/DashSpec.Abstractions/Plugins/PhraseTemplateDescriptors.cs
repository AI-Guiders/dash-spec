namespace DashSpec.Abstractions.Plugins;

public static class PhraseScopes
{
    public const string OnClick = "card.on_click";

    public const string Button = "card.button";

    public const string ExtensionBlock = "card.extension";
}

public enum PhraseSlotKind
{
    Ident,
    String,
    Int,
}

public sealed record PhraseSlotDescriptor(
    string Name,
    PhraseSlotKind Kind,
    bool Optional = false);

public sealed record PhraseTemplateDescriptor(
    string PluginId,
    string HandlerId,
    string Scope,
    string Pattern,
    IReadOnlyList<PhraseSlotDescriptor> Slots);

public sealed record ScopeContributorDescriptor(
    string PluginId,
    string ScopeId,
    string ContainerKeyword,
    string? ParentScopeId,
    IReadOnlyList<string> CoreKeywords);
