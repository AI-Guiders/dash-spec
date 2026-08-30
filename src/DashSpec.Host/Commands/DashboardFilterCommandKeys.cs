#nullable enable
using Microsoft.AspNetCore.Components.Web;

namespace DashSpec.Host.Commands;

internal static class DashboardFilterCommandKeys
{
    public static bool IsAcceptCompletion(KeyboardEventArgs e) =>
        e.Key == "Tab"
        || (e.Key == " " && e.CtrlKey && !e.AltKey && !e.MetaKey && !e.ShiftKey);
}
