
using DashSpec.Abstractions.Viewer;
using DashSpec.Execution.Parsing;
using DashSpec.Execution.Runtime;
using DashSpec.Modeling.Parse.Layout;
using ExecutionLayoutModuleParser = DashSpec.Execution.Parsing.LayoutModuleParser;

namespace DashSpec.Host.Configuration;

/// <summary>
/// Planet-content Host shell from the host module (ADR-0054 / [ADR-0111] §2). The shell grid
/// comes from this module's scope-host board; the embedded <c>host.ds</c> default board serves
/// when the module carries none — one resolver (<see cref="ShellBoardSlotResolver"/>) places both.
/// </summary>
public sealed class HostShellBootstrap
{
    private const string EmbeddedResourceName = "DashSpec.Host.Resources.host.ds";

    public required HostDocument Document { get; init; }

    public required string FullPath { get; init; }

    public string ProductTitle => GetPresentation("product_title") ?? "DashSpec";

    public string CatalogLabel => GetPresentation("catalog_label") ?? "Отчёт";

    /// <summary>
    /// Shell grid slots ([ADR-0111] §2): the scope-host board from this module, or the embedded
    /// <c>host.ds</c> default — the host renders the shell grid from these slots, content first.
    /// </summary>
    public IReadOnlyList<ViewerShellSlot> ShellSlots =>
        ShellBoardSlotResolver.Resolve(Document.ShellLayout?.Value ?? EmbeddedDefaultBoard());

    public bool ShowsSurface(string surface) =>
        Document.Surfaces.Count == 0
        || Document.Surfaces.Contains(surface, StringComparer.OrdinalIgnoreCase);

    private LayoutBoardDefinition? _embeddedDefaultBoard;

    /// <summary>
    /// The embedded default shell board (<c>host.ds</c>): a regular ds layout module —
    /// brand + nav topbar row, content below — not code.
    /// </summary>
    private LayoutBoardDefinition EmbeddedDefaultBoard()
    {
        if (_embeddedDefaultBoard is { } board)
        {
            return board;
        }

        using var stream = typeof(HostShellBootstrap).Assembly
            .GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded default shell board not found: '{EmbeddedResourceName}'.");

        using var reader = new StreamReader(stream);
        return _embeddedDefaultBoard = ExecutionLayoutModuleParser.ParseLayoutFile(reader.ReadToEnd());
    }

    private string? GetPresentation(string key) =>
        Document.Presentation.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
}
