#nullable enable

using Microsoft.AspNetCore.Components.Web;

namespace DashSpec.Host.Commands;

/// <summary>Keyboard bindings from <c>dash.catalog</c> bindings table (GUIDERS-ADR-0047).</summary>
internal static class DashboardCatalogBindings
{
    public const string ChordRootRole = "chord-root";
    public const string SuggestDismissRole = "suggest-dismiss";

    public static string ChordRootGesture =>
        ResolveRoleGesture(ChordRootRole)
        ?? DashboardCatalog.Current.Defaults.BindingChordRoot
        ?? "Ctrl+K";

    public static string? SuggestDismissGesture =>
        ResolveRoleGesture(SuggestDismissRole);

    public static IReadOnlyList<CatalogHostBinding> HostBindings()
    {
        var bindings = new List<CatalogHostBinding>
        {
            new(ChordRootGesture, "OnChordRoot"),
        };

        var dismiss = SuggestDismissGesture;
        if (!string.IsNullOrWhiteSpace(dismiss))
        {
            bindings.Add(new(dismiss, "OnSuggestDismiss"));
        }

        return bindings;
    }

    public static bool MatchesGesture(KeyboardEventArgs e, string? gestureWire)
    {
        if (string.IsNullOrWhiteSpace(gestureWire))
        {
            return false;
        }

        var parts = gestureWire.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        var keyToken = parts[^1];
        var needCtrl = parts.Any(static part =>
            part.Equals("ctrl", StringComparison.OrdinalIgnoreCase)
            || part.Equals("control", StringComparison.OrdinalIgnoreCase));
        var needAlt = parts.Any(static part => part.Equals("alt", StringComparison.OrdinalIgnoreCase));
        var needMeta = parts.Any(static part =>
            part.Equals("meta", StringComparison.OrdinalIgnoreCase)
            || part.Equals("cmd", StringComparison.OrdinalIgnoreCase)
            || part.Equals("command", StringComparison.OrdinalIgnoreCase));
        var needShift = parts.Any(static part => part.Equals("shift", StringComparison.OrdinalIgnoreCase));

        if (e.CtrlKey != needCtrl
            || e.AltKey != needAlt
            || e.MetaKey != needMeta
            || e.ShiftKey != needShift)
        {
            return false;
        }

        return keyToken.Length == 1
            ? string.Equals(e.Key, keyToken, StringComparison.OrdinalIgnoreCase)
            : string.Equals(e.Key, keyToken, StringComparison.OrdinalIgnoreCase);
    }

    static string? ResolveRoleGesture(string role) =>
        DashboardCatalog.Current.Bindings
            .FirstOrDefault(row => string.Equals(row.Role, role, StringComparison.OrdinalIgnoreCase))
            ?.Gesture;
}

internal readonly record struct CatalogHostBinding(string Gesture, string MethodName);
