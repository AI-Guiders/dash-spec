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
}

/// <summary>Host-level render stack selection (ADR-0112 §6): filled from host bootstrap.</summary>
public interface IRenderStackSettings
{
    /// <summary>Effective host stack name; empty/unknown = selfmade.</summary>
    string HostStack { get; }

    /// <summary>Color scheme token (existing <c>color_scheme</c>): each stack reads it its own way.</summary>
    string ColorScheme { get; }
}

/// <summary>Mutable singleton behind <see cref="IRenderStackSettings"/> — filled once at host bootstrap.</summary>
public sealed class RenderStackSettings : IRenderStackSettings
{
    public string HostStack { get; set; } = "selfmade";

    public string ColorScheme { get; set; } = "dark";
}
