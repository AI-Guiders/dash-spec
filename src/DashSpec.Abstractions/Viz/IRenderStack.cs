namespace DashSpec.Abstractions.Viz;

/// <summary>
/// Render stack (ADR-0112): the visual language of the surface. The stack routes each
/// data family to a renderer and may delegate a single kind to a guest renderer
/// (Radzen stack: "heatmap is not mine — builtin renders it"). The author never picks
/// a renderer; the stack is chosen once (host module <c>presentation render =</c>,
/// spec <c>configuration render =</c> override).
/// </summary>
public interface IRenderStack
{
    /// <summary>Stack name as used in <c>render =</c> (e.g. <c>selfmade</c>, <c>radzen</c>).</summary>
    string Name { get; }

    /// <summary>
    /// Data family name (<c>Chart</c>, <c>Table</c>, <c>Scalar</c>, <c>Matrix</c>, <c>Gantt</c>;
    /// case-insensitive) → renderer plugin id. The stack's own routing.
    /// </summary>
    IReadOnlyDictionary<string, string> FamilyPlugins { get; }

    /// <summary>Kind id → renderer plugin id: the stack delegates this kind to a guest.</summary>
    IReadOnlyDictionary<string, string> KindPlugins { get; }

    /// <summary>Themes this stack can render (ADR-0112 §2).</summary>
    IReadOnlyList<ViewerTheme> Themes => [];

    /// <summary>Default theme token for this stack.</summary>
    string DefaultThemeToken => "dark";

    /// <summary>Head component type mounting this stack's CSS assets.</summary>
    Type? HeadComponent => null;

    /// <summary>Scripts component type mounting this stack's JS assets.</summary>
    Type? ScriptsComponent => null;
}

/// <summary>A theme the active stack can render (ADR-0112 §2): the token is the
/// author's/viewer's word, the label is for pickers. The dictionary itself lives in
/// the stack; the host copies it here for surface chrome (the per-user picker).</summary>
public sealed record ViewerTheme(string Token, string Label);

/// <summary>Host-level render stack selection (ADR-0112 §6): filled from host bootstrap.</summary>
public interface IRenderStackSettings
{
    /// <summary>Viewer-layer cookie name for per-user theme override.</summary>
    public const string ViewerCookieName = "dashspec_color_scheme";

    /// <summary>Effective host stack name; empty/unknown = selfmade.</summary>
    string HostStack { get; }

    /// <summary>Color scheme token (existing <c>color_scheme</c>): each stack reads it its own way.</summary>
    string ColorScheme { get; }

    /// <summary>Default theme token for the active stack.</summary>
    string DefaultThemeToken => "dark";

    /// <summary>Themes the active stack can render (empty = the stack does not read the
    /// token; surface chrome hides the picker). The admin sets the host default; each
    /// viewer may override it locally (cookie).</summary>
    IReadOnlyList<ViewerTheme> Themes { get; }

    /// <summary>Head component type for the active stack.</summary>
    Type? HeadComponent => null;

    /// <summary>Scripts component type for the active stack.</summary>
    Type? ScriptsComponent => null;
}

/// <summary>Mutable singleton behind <see cref="IRenderStackSettings"/> — filled once at host bootstrap.</summary>
public sealed class RenderStackSettings : IRenderStackSettings
{
    public string HostStack { get; set; } = "selfmade";

    public string ColorScheme { get; set; } = "dark";

    public string DefaultThemeToken { get; set; } = "dark";

    public IReadOnlyList<ViewerTheme> Themes { get; set; } = [];

    public Type? HeadComponent { get; set; }

    public Type? ScriptsComponent { get; set; }
}
