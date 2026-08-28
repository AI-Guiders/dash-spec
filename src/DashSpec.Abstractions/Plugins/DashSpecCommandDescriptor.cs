namespace DashSpec.Abstractions.Plugins;

/// <summary>Slash command descriptor for plugin catalog merge (DASHSPEC-ADR-0043 W4).</summary>
public sealed record DashSpecCommandDescriptor(
    string PluginId,
    string CommandId,
    string Path,
    string? Help = null,
    string ArgTail = "required",
    IReadOnlyList<string>? PathAliases = null,
    string? Group = null);
