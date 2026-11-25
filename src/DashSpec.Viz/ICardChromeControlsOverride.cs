using DashSpec.Abstractions.Viewer;
using DashSpec.Execution.Runtime;
using Microsoft.AspNetCore.Components;

namespace DashSpec.Viz;

/// <summary>One view of a card's view switcher (ADR-0118 addressability: views-{cardId}).</summary>
public sealed record CardViewOption(string Id, string Label);

/// <summary>Typed parameter contract for stack-bound card chrome buttons (ADR-0112).</summary>
public sealed class StackButtonContext
{
    public required string Label { get; init; }

    public bool Disabled { get; init; }

    public EventCallback Clicked { get; init; }

    /// <summary>Toggle buttons (aria-pressed); null — a plain button.</summary>
    public bool? Pressed { get; init; }

    /// <summary>The plugin's own glyph (svg) — the role declares WHAT the button does,
    /// the stack paints the button around it.</summary>
    public RenderFragment? Icon { get; init; }

    /// <summary>Automation handle (data-testid); null — the button stays anonymous.</summary>
    public string? TestId { get; init; }
}

/// <summary>Typed parameter contract for the stack-bound card view-switcher (ADR-0112).</summary>
public sealed class StackViewSwitchContext
{
    public required IReadOnlyList<CardViewOption> Views { get; init; }

    public required string Active { get; init; }

    public EventCallback<string> ActiveChanged { get; init; }

    public bool Disabled { get; init; }

    public string AriaLabel { get; init; } = string.Empty;
}

/// <summary>Typed parameter contract for stack-bound sidebar navigation (ADR-0112 §5).</summary>
public sealed class StackSidebarContext
{
    public IReadOnlyList<CatalogEntryDefinition> Entries { get; init; } = [];

    public IReadOnlyList<CatalogGroupDefinition> Groups { get; init; } = [];

    public string? ActiveEntryId { get; init; }

    public bool Busy { get; init; }

    public string SidebarLabel { get; init; } = string.Empty;

    public IReadOnlyList<TabDefinition> Tabs { get; init; } = [];

    public Func<TabDefinition, string>? ResolveTabLabel { get; init; }

    public string? ActiveTabId { get; init; }

    public ViewerMenuSectionsMode SectionsMode { get; init; } = ViewerMenuSectionsMode.All;

    public bool CollapseDuplicateSection { get; init; }

    public EventCallback<string> OnSelectEntry { get; init; }

    public EventCallback<string> OnSelectTab { get; init; }
}

/// <summary>Typed parameter contract for stack-bound horizontal tab bar (ADR-0112 §5).</summary>
public sealed class StackTabBarContext
{
    public IReadOnlyList<TabDefinition> Tabs { get; init; } = [];

    public Func<TabDefinition, string>? ResolveLabel { get; init; }

    public string? ActiveTabId { get; init; }

    public bool Sticky { get; init; }

    public EventCallback<string> OnSelect { get; init; }
}

/// <summary>Typed parameter contract for stack-bound documentation portal (ADR-0138 / ADR-0112 §5).</summary>
public sealed class StackDocPortalContext
{
    public required DashSpec.Modeling.Parse.Syntax.DashSpecCompiledBook Book { get; init; }

    public required DashSpec.Modeling.Parse.Syntax.DashSpecCompiledBookPage? ActivePage { get; init; }

    public required DashSpec.Modeling.Parse.Syntax.DashSpecSymbolDoc? ActiveSymbol { get; init; }

    public required string Search { get; init; }

    public required bool TreeCollapsed { get; init; }

    public required bool TocCollapsed { get; init; }

    public required ISet<string> ExpandedPages { get; init; }

    public required EventCallback<string> OnSearchChanged { get; init; }

    public required EventCallback OnToggleTree { get; init; }

    public required EventCallback OnToggleToc { get; init; }

    public required EventCallback<DashSpec.Modeling.Parse.Syntax.DashSpecCompiledBookPage> OnSelectChapter { get; init; }

    public required EventCallback<DashSpec.Modeling.Parse.Syntax.DashSpecCompiledBookPage> OnToggleChapter { get; init; }

    public required EventCallback<(DashSpec.Modeling.Parse.Syntax.DashSpecCompiledBookPage? Page, DashSpec.Modeling.Parse.Syntax.DashSpecSymbolDoc Symbol)> OnSelectSymbol { get; init; }

    public required EventCallback<string> OnNavigateToSymbolId { get; init; }

    public required Func<string, RenderFragment> RenderRichText { get; init; }

    public required Func<string, string, RenderFragment> RenderEmbeddedReport { get; init; }
}

/// <summary>
/// ADR-0112: the card and shell chrome controls (the view-switcher selector, the extension
/// buttons, the navigation sidebar/tabbar) are stack-replaceable visuals. The surface owns
/// the semantics; a stack may bind its own controls implementing these parameter surfaces.
/// Absent override — the surface's native markup renders.
/// </summary>
public interface ICardChromeControlsOverride
{
    /// <summary>Component for the view-switcher surface (<see cref="StackViewSwitchContext"/>).
    /// Null — keep the native select markup.</summary>
    Type? ResolveViewSwitch();

    /// <summary>Component for the button surface (<see cref="StackButtonContext"/>).
    /// Null — keep the native button markup.</summary>
    Type? ResolveButton();

    /// <summary>Component for the left navigation sidebar (<see cref="StackSidebarContext"/>).
    /// Null — the stack uses topbar catalog + horizontal tab bar instead of a left sidebar.</summary>
    Type? ResolveSidebar() => null;

    /// <summary>Component for the horizontal tab bar (<see cref="StackTabBarContext"/>).
    /// Null — keep the native button tab bar markup.</summary>
    Type? ResolveTabBar() => null;

    /// <summary>Component for the documentation portal (<see cref="StackDocPortalContext"/>).
    /// Null — keep the native documentation portal markup.</summary>
    Type? ResolveDocPortal() => null;
}

/// <summary>Default: no overrides (the surface's native chrome markup renders).</summary>
public sealed class DefaultCardChromeControlsOverride : ICardChromeControlsOverride
{
    public Type? ResolveViewSwitch() => null;

    public Type? ResolveButton() => null;

    public Type? ResolveSidebar() => null;

    public Type? ResolveTabBar() => null;
}
