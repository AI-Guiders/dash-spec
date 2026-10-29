namespace DashSpec.Abstractions.Viewer;

/// <summary>Shell slot ids ([ADR-0111] §2): chrome slots + the content slot, from the host module board.</summary>
public static class ViewerShellSlotIds
{
    public const string Brand = "brand";
    public const string Catalog = "catalog";
    public const string ReportPicker = "report_picker";
    public const string Nav = "nav";
    public const string Dashboard = "dashboard";
    public const string ExternalLinks = "external_links";
    public const string Help = "help";
    public const string Settings = "settings";
    public const string SpecDev = "spec_dev";
    public const string Content = "content";
}

/// <summary>
/// One shell grid slot ([ADR-0111] §2): the host renders the shell grid from the host module's
/// scope-host layout board — slot id plus grid placement (row/col/span), as recorded in ds.
/// </summary>
public sealed record ViewerShellSlot(string SlotId, int Row, int Col, int Span);

/// <summary>How the menu nests an entry's sections (the `menu` block of the host module, ADR-0073).</summary>
public enum ViewerMenuSectionsMode
{
    /// <summary>Nested sections under every entry (the default).</summary>
    All = 0,

    /// <summary>Siblings only: a pinned section (`section X` in the catalog entry) is not
    /// repeated — the entry itself represents it.</summary>
    Siblings = 1,

    /// <summary>No nested sections; entries are leaves.</summary>
    None = 2,
}

/// <summary>Menu policy from the host module's `menu` block — explicit author intent,
/// no host-side heuristics (ADR-0074 §2).</summary>
public sealed record ViewerMenuPolicy(
    ViewerMenuSectionsMode Sections = ViewerMenuSectionsMode.All,
    bool CollapseDuplicateSection = false);

/// <summary>Host shell chrome from the host module (ADR-0054 / [ADR-0111] §2).</summary>
public interface IViewerShellChrome
{
    string ProductTitle { get; }

    string CatalogLabel { get; }

    IReadOnlyList<ViewerShellSlot> ShellSlots { get; }

    bool ShowsSurface(string surface);

    ViewerMenuPolicy Menu { get; }
}

public interface IViewerPresentationOptions
{
    string? Language { get; }

    string ColorScheme { get; }

    string LargeFieldFilterLayout { get; }
}
