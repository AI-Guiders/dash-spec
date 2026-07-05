using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class LayoutModuleScopeValidator
{
    public static void EnsureMatchesIncludeSite(
        LayoutBoardDefinition board,
        LayoutScope expected,
        string context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(context);

        if (board.ModuleScope is null)
        {
            return;
        }

        if (board.ModuleScope != expected)
        {
            throw new DashSpecParseException(
                $"{context}: layout module declares scope {FormatScope(board.ModuleScope.Value)} " +
                $"but was included for {FormatScope(expected)}.");
        }
    }

    private static string FormatScope(LayoutScope scope) =>
        scope switch
        {
            LayoutScope.Toolbar => "toolbar",
            LayoutScope.Tab => "tab",
            LayoutScope.Card => "card",
            _ => scope.ToString(),
        };
}
