using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class ToolbarPlacementParser
{
    public static void Parse(TokenReader reader, DashboardShellContext ctx, string blockName) =>
        Parse(reader, blockName, board => ctx.AssignToolbarBoard(board, blockName), names => ctx.DashboardFilters.AddRange(names));

    public static void Discard(TokenReader reader, string blockName) =>
        Parse(reader, blockName, _ => { }, _ => { });

    private static void Parse(
        TokenReader reader,
        string blockName,
        Action<LayoutBoardDefinition> onBoard,
        Action<IReadOnlyList<string>> onFlatNames)
    {
        var (endKind, endId) = ResolveEndKind(blockName);

        if (reader.IsAt(TokenKind.LBracket))
        {
            onBoard(LayoutParser.ParseBoardRows(reader));
            return;
        }

        if (reader.IsOnNewline())
        {
            BlockSyntax.BeginBlock(reader);
            reader.SkipNewlines();
            if (reader.IsAt(TokenKind.LBracket))
            {
                onBoard(LayoutParser.ParseBoardRows(reader, endKind, endId));
                BlockSyntax.ExpectBlockEnd(reader, endKind, endId);
                return;
            }

            onFlatNames(ParseNameListUntilEnd(reader, endKind, endId, blockName));
            return;
        }

        onFlatNames(reader.ReadCommaListInline());
    }

    private static (string kind, string? id) ResolveEndKind(string blockName)
    {
        var parts = blockName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 &&
            string.Equals(parts[0], "layout", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(parts[1], "board", StringComparison.OrdinalIgnoreCase))
        {
            return ("layout", "board");
        }

        return (parts[^1], null);
    }

    private static IReadOnlyList<string> ParseNameListUntilEnd(
        TokenReader reader,
        string endKind,
        string? endId,
        string blockName)
    {
        var names = new List<string>();
        while (!BlockSyntax.IsBlockEnd(reader, endKind, endId) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (BlockSyntax.IsBlockEnd(reader, endKind, endId))
            {
                break;
            }

            names.Add(reader.ReadIdent());
            reader.SkipNewlines();
            if (reader.CurrentKind is TokenKind.Comma)
            {
                reader.Advance();
            }
        }

        reader.SkipNewlines();
        BlockSyntax.ExpectBlockEnd(reader, endKind, endId);
        if (names.Count == 0)
        {
            throw new DashSpecParseException($"{blockName} block requires at least one name.");
        }

        return names;
    }
}
