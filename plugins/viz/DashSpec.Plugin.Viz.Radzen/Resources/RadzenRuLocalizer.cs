using System.Globalization;
using System.Resources;
using Radzen;

namespace DashSpec.Plugin.Viz.RadzenUI.Resources;

/// <summary>
/// Russian strings for Radzen's built-in chrome (ADR-0115 §3): RadzenStrings ships satellites
/// for de/es/fr/it/ja/pt only — no ru — so this localizer fills the gap. Unknown keys and
/// non-Russian cultures return null → Radzen's own default (graceful fallback).
/// The resx is culture-neutral on purpose: ru-ness is decided here, not by culture probing.
/// </summary>
public sealed class RadzenRuLocalizer : ILocalizer
{
    private static readonly ResourceManager Strings =
        new("DashSpec.Plugin.Viz.RadzenUI.Resources.RadzenRuStrings", typeof(RadzenRuLocalizer).Assembly);

    public string? Get(string key, CultureInfo culture) =>
        culture.Name.StartsWith("ru", StringComparison.OrdinalIgnoreCase)
            ? Strings.GetString(key)
            : null;
}
