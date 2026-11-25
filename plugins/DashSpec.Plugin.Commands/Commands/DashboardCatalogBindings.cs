#nullable enable

using AIGuiders.Platform.Modeling.Notations.Keyboard;
using DashSpec.Modeling.Parse.Hotkeys;
using Microsoft.AspNetCore.Components.Web;

namespace DashSpec.Surface.Blazor.Commands;

/// <summary>
/// Keyboard bindings from <c>stdlib/hotkeys/hotkeys.ds</c> and <c>dash.catalog</c> bindings table,
/// normalized via <c>AIGuiders.Platform.Notations.Keyboard.*</c> (<see cref="NormalizedKeySequence"/>).
/// </summary>
internal static class DashboardCatalogBindings
{
    public const string ChordRootRole = "chord-root";
    public const string SuggestDismissRole = "suggest-dismiss";

    public static string ChordRootGesture =>
        ResolveRoleGesture(ChordRootRole)
        ?? DashCatalog.Document.Defaults.BindingChordRoot
        ?? throw new InvalidOperationException("dash.catalog missing chord-root gesture.");

    public static string? SuggestDismissGesture =>
        ResolveRoleGesture(SuggestDismissRole);

    public static IReadOnlyList<CatalogHostBinding> HostBindings()
    {
        var bindings = new List<CatalogHostBinding>
        {
            new(NormalizeToCanonicalWire(ChordRootGesture), "OnChordRoot"),
        };

        var dismiss = SuggestDismissGesture;
        if (!string.IsNullOrWhiteSpace(dismiss))
        {
            bindings.Add(new(NormalizeToCanonicalWire(dismiss), "OnSuggestDismiss"));
        }

        return bindings;
    }

    public static string NormalizeToCanonicalWire(string gestureWire)
    {
        var parsed = HotkeyGestureNormalizer.tryParseGesture(HotkeyNotationKind.Auto, gestureWire);
        return parsed.IsOk
            ? HotkeyGestureNormalizer.toCanonicalKeyGesture(parsed.ResultValue)
            : gestureWire;
    }

    public static bool MatchesGesture(KeyboardEventArgs e, string? gestureWire)
    {
        if (string.IsNullOrWhiteSpace(gestureWire))
        {
            return false;
        }

        var parsed = HotkeyGestureNormalizer.tryParseGesture(HotkeyNotationKind.Auto, gestureWire);
        if (!parsed.IsOk || parsed.ResultValue.Steps.Count == 0)
        {
            return false;
        }

        return MatchesNormalizedStep(e, parsed.ResultValue.Steps[0]);
    }

    public static bool MatchesNormalizedStep(KeyboardEventArgs e, NormalizedSequenceStep step)
    {
        ChordModifierKeys mods;
        string keySymbol;

        switch (step)
        {
            case NormalizedChordStep chord:
                mods = chord.Modifiers;
                keySymbol = chord.KeySymbol;
                break;
            case NormalizedPlainKeyStep plain:
                mods = ChordModifierKeys.None;
                keySymbol = plain.KeySymbol;
                break;
            default:
                return false;
        }

        var needCtrl = mods.HasFlag(ChordModifierKeys.Control);
        var needAlt = mods.HasFlag(ChordModifierKeys.Alt);
        var needShift = mods.HasFlag(ChordModifierKeys.Shift);
        var needMeta = mods.HasFlag(ChordModifierKeys.Meta);

        if (e.CtrlKey != needCtrl
            || e.AltKey != needAlt
            || e.ShiftKey != needShift
            || e.MetaKey != needMeta)
        {
            return false;
        }

        return string.Equals(e.Key, keySymbol, StringComparison.OrdinalIgnoreCase);
    }

    static string? ResolveRoleGesture(string role) =>
        DashCatalog.Document.Bindings
            .FirstOrDefault(row => string.Equals(row.Role, role, StringComparison.OrdinalIgnoreCase))
            ?.Gesture;
}

internal readonly record struct CatalogHostBinding(string Gesture, string MethodName);
