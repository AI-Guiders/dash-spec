using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

namespace DashSpec.Core.Parsing;

internal static class ToolbarPlacementResolver
{
    public static IReadOnlyList<string> ResolveFilterNames(
        IReadOnlyList<FilterDefinition> filters,
        IReadOnlyList<string> flatNames,
        LayoutBoardDefinition? board)
    {
        if (board is null)
        {
            return flatNames;
        }

        if (flatNames.Count > 0)
        {
            throw new DashSpecParseException(
                "Toolbar cannot combine a layout board with a flat filter list.");
        }

        const string context = "Toolbar";
        var names = new List<string>();
        foreach (var row in board.Rows)
        {
            foreach (var token in row)
            {
                var name = FilterLayoutRefResolver.Resolve(token, filters, context);
                if (names.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    throw new DashSpecParseException(
                        $"{context}: filter '{name}' appears more than once in the toolbar board.");
                }

                names.Add(name);
            }
        }

        if (names.Count == 0)
        {
            throw new DashSpecParseException($"{context} layout board requires at least one filter.");
        }

        return names;
    }
}
