#nullable enable
using Microsoft.AspNetCore.Components.Web;

namespace DashSpec.Host.Commands;

internal static class DashboardFilterCommandKeys
{
    public static bool IsAcceptCompletion(KeyboardEventArgs e) =>
        e.Key == "Tab"
        || (e.Key == " " && e.CtrlKey && !e.AltKey && !e.MetaKey && !e.ShiftKey);

    /// <summary>Capture-phase preventDefault when suggestions are open (see @aiguiders/input surfaces.commandLine).</summary>
    public static bool PreventDefaultWhenSuggestOpen(KeyboardEventArgs e, bool suggestOpen) =>
        suggestOpen
        && (e.Key == "Tab"
            || (e.Key == " " && e.CtrlKey && !e.AltKey && !e.MetaKey && !e.ShiftKey));
}
