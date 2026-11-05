namespace DashSpec.Stack.Radzen;

/// <summary>One theme of the Radzen stack (ADR-0112 §2): the token is the author's
/// word; the reading — which Radzen base css carries it and whether the stack's own
/// palette override rides on top — is entirely the stack's.</summary>
public sealed record RadzenTheme(
    string Token,
    string Label,
    string BaseCss,
    string? OverrideCss = null);

/// <summary>The Radzen stack's theme dictionary (ADR-0112 §2/«темы — плагины»): the
/// base set of popular words the stack carries itself. An unknown token is the stack
/// default + a loud bootstrap warning — never a silent lie (the old admin option
/// "High contrast" rendered plain dark). Theme plugins may extend the dictionary
/// later; this set is what ships with the stack.</summary>
public static class RadzenColorSchemes
{
    public const string DefaultToken = "dark";

    /// <summary>The viewer-layer cookie: a per-user override of the host default
    /// (the shell picker writes it; App.razor reads it on every page load).</summary>
    public const string ViewerCookieName = "dashspec_color_scheme";

    /// <summary>The base css files ship inside the Radzen.Blazor package; the version
    /// parameter pins the cache to the package version.</summary>
    private const string V = "12.1.5-4";

    public static readonly RadzenTheme Dark = new(
        "dark", "Морская (тёмная)", $"_content/Radzen.Blazor/css/dark.css?v={V}",
        $"_content/DashSpec.Stack.Radzen/css/theme-dark.css?v=0.1.0");

    public static readonly RadzenTheme Light = new(
        "light", "Морская (светлая)", $"_content/Radzen.Blazor/css/default.css?v={V}",
        $"_content/DashSpec.Stack.Radzen/css/theme-light.css?v=0.1.0");

    public static readonly IReadOnlyList<RadzenTheme> All =
    [
        Dark,
        Light,
        new RadzenTheme("material", "Material (светлая)", $"_content/Radzen.Blazor/css/material.css?v={V}"),
        new RadzenTheme("material_dark", "Material (тёмная)", $"_content/Radzen.Blazor/css/material-dark.css?v={V}"),
    ];

    /// <summary>The stack's reading of the token: empty/null → the default; a known
    /// token → its theme; anything else → null (the caller decides the loud path).</summary>
    public static RadzenTheme? Find(string? token)
    {
        var normalized = (token ?? string.Empty).Trim();
        return normalized.Length == 0
            ? Dark
            : All.FirstOrDefault(t => string.Equals(t.Token, normalized, StringComparison.OrdinalIgnoreCase));
    }
}
