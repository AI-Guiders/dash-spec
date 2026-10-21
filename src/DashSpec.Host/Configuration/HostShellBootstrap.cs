
using DashSpec.Abstractions.Viewer;

namespace DashSpec.Host.Configuration;

/// <summary>Planet-content Host shell from the host module (ADR-0054 / [ADR-0111] §2).</summary>
public sealed class HostShellBootstrap
{
    public required HostDocument Document { get; init; }

    public required string FullPath { get; init; }

    public string ProductTitle => GetPresentation("product_title") ?? "DashSpec";

    public string CatalogLabel => GetPresentation("catalog_label") ?? "Отчёт";

    /// <summary>
    /// Shell grid slots ([ADR-0111] §2): the scope-host board from this module, or the embedded
    /// <c>host.ds</c> default — the host renders the shell grid from these slots, content first.
    /// </summary>
    public IReadOnlyList<ViewerShellSlot> ShellSlots =>
        HostShellBoardSlots.Resolve(Document.ShellLayout?.Value);
    public bool ShowsSurface(string surface) =>
        Document.Surfaces.Count == 0
        || Document.Surfaces.Contains(surface, StringComparer.OrdinalIgnoreCase);

    private string? GetPresentation(string key) =>
        Document.Presentation.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
}
