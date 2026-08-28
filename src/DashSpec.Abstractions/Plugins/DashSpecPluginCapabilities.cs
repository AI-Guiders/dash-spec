namespace DashSpec.Abstractions.Plugins;

public sealed class DashSpecPluginCapabilities
{
    public string Bundle { get; set; } = string.Empty;

    public List<LoadedPluginCapability> Plugins { get; set; } = [];

    public List<string> DiagramKinds { get; set; } = [];

    public List<string> ExtensionBlocks { get; set; } = [];

    public List<string> InteractionHandlers { get; set; } = [];

    public List<string> ActionHandlers { get; set; } = [];

    public List<string> VizRenderers { get; set; } = [];

    public List<string> PhraseScopes { get; set; } = [];

    public List<string> PhraseTemplates { get; set; } = [];

    public List<string> FilterWidgets { get; set; } = [];

    public List<string> CardChromeBlocks { get; set; } = [];

    public List<DashSpecCommandCapability> Commands { get; set; } = [];
}

public sealed class DashSpecCommandCapability
{
    public string PluginId { get; set; } = string.Empty;

    public string CommandId { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string? Help { get; set; }

    public string ArgTail { get; set; } = "required";

    public List<string> PathAliases { get; set; } = [];

    public string? Group { get; set; }
}

public sealed class LoadedPluginCapability
{
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Tier { get; set; } = string.Empty;

    public List<string> DiagramKinds { get; set; } = [];

    public List<string> ExtensionBlocks { get; set; } = [];

    public List<string> InteractionHandlers { get; set; } = [];

    public List<string> ActionHandlers { get; set; } = [];

    public List<string> PhraseTemplates { get; set; } = [];

    public List<string> FilterWidgets { get; set; } = [];

    public List<string> CardChromeBlocks { get; set; } = [];
}
