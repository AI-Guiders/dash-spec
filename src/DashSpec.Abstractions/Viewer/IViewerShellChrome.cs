namespace DashSpec.Abstractions.Viewer;

public static class ViewerTopbarSlots
{
    public const string Catalog = "catalog";
    public const string Nav = "nav";
    public const string ExternalLinks = "external_links";
    public const string Help = "help";
    public const string Settings = "settings";
    public const string SpecDev = "spec_dev";
}

/// <summary>Host shell chrome from <c>.dashhost</c> (ADR-0054 / B3.3 port).</summary>
public interface IViewerShellChrome
{
    string ProductTitle { get; }

    string CatalogLabel { get; }

    IReadOnlyList<string> TopbarSlots { get; }

    bool ShowsSurface(string surface);
}

public interface IViewerPresentationOptions
{
    string? Language { get; }

    string ColorScheme { get; }

    string LargeFieldFilterLayout { get; }
}
