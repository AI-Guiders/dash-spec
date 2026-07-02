using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class ToolbarPlacementParser
{
    public static void Parse(TokenReader reader, DashboardShellContext ctx, string blockName)
    {
        Parse(reader, blockName, board => ctx.AssignToolbarBoard(board, blockName), names => ctx.DashboardFilters.AddRange(names));
    }

    public static void Discard(TokenReader reader, string blockName) =>
        Parse(reader, blockName, _ => { }, _ => { });

    private static void Parse(
        TokenReader reader,
        string blockName,
        Action<LayoutBoardDefinition> onBoard,
        Action<IReadOnlyList<string>> onFlatNames)
    {
        if (reader.IsAt(TokenKind.LBracket))
        {
            onBoard(LayoutParser.ParseBoardRows(reader));
            return;
        }

        if (reader.IsAt(TokenKind.LBrace))
        {
            reader.Advance();
            reader.SkipNewlines();
            if (reader.IsAt(TokenKind.LBracket))
            {
                onBoard(LayoutParser.ParseBoardRows(reader));
                reader.SkipNewlines();
                reader.Expect(TokenKind.RBrace);
                return;
            }

            onFlatNames(ParseCommaListFromOpenBrace(reader, blockName));
            return;
        }

        onFlatNames(reader.ReadCommaListInline());
    }

    private static IReadOnlyList<string> ParseCommaListFromOpenBrace(TokenReader reader, string blockName)
    {
        reader.SkipNewlines();

        var names = new List<string>();
        while (!reader.IsAt(TokenKind.RBrace) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (reader.IsAt(TokenKind.RBrace))
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
        reader.Expect(TokenKind.RBrace);
        if (names.Count == 0)
        {
            throw new DashSpecParseException($"{blockName} block requires at least one name.");
        }

        return names;
    }
}
