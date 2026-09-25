using DashSpec.Core.Model;

namespace DashSpec.Host.Configuration;

/// <summary>Planet-content Host shell from <c>.dashhost</c> (ADR-0054).</summary>
public sealed class HostShellBootstrap
{
    public required HostDocument Document { get; init; }

    public required string FullPath { get; init; }

    public string ProductTitle => GetPresentation("product_title") ?? "DashSpec";

    public string CatalogLabel => GetPresentation("catalog_label") ?? "Отчёт";

    public bool ShowsSurface(string surface) =>
        Document.Surfaces.Count == 0
        || Document.Surfaces.Contains(surface, StringComparer.OrdinalIgnoreCase);

    private string? GetPresentation(string key) =>
        Document.Presentation.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
}
