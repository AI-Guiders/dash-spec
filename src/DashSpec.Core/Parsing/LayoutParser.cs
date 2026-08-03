using DashSpec.Core.Model;



namespace DashSpec.Core.Parsing;



internal static class LayoutParser

{

    public static LayoutDefinition ParseGrid(TokenReader reader)

    {

        _ = reader.ReadIdent() switch

        {

            "grid" => true,

            _ => throw reader.Unexpected("grid"),

        };



        var props = PropertyBlockParser.Parse(reader, PropertySchemas.LayoutGrid, "layout grid");



        var columns = LayoutDefinition.Default.Columns;

        var gap = LayoutDefinition.Default.GapPx;



        if (props.TryGetValue("columns", out var columnsRaw) &&

            int.TryParse(columnsRaw, out var parsedColumns) &&

            parsedColumns is > 0 and <= 24)

        {

            columns = parsedColumns;

        }



        if (props.TryGetValue("gap", out var gapRaw) &&

            int.TryParse(gapRaw, out var parsedGap) &&

            parsedGap >= 0)

        {

            gap = parsedGap;

        }



        return new LayoutDefinition(columns, gap);

    }



    /// <summary>Tab-level bracket board: layout … end layout.</summary>

    public static LayoutBoardDefinition ParseBoard(TokenReader reader)

    {

        BlockSyntax.BeginBlock(reader);

        reader.SkipNewlines();

        var board = ParseBoardRows(reader, "layout");

        BlockSyntax.ExpectBlockEnd(reader, "layout");

        return board;

    }



    /// <summary>Bracket rows until EOF or end kind (for .dashlayout modules).</summary>

    public static LayoutBoardDefinition ParseBoardRows(TokenReader reader, string? endKind = null, string? endId = null)
    {
        var rows = new List<IReadOnlyList<string>>();
        reader.SkipNewlines();

        while (!reader.IsEof &&
               !reader.IsAt(TokenKind.RBrace) &&
               (endKind is null || !BlockSyntax.IsBlockEnd(reader, endKind, endId)))
        {
            if (!reader.IsAt(TokenKind.LBracket))
            {
                throw reader.Unexpected("[");
            }

            rows.Add(ParseBoardRow(reader));
            reader.SkipNewlines();
        }



        if (rows.Count == 0)

        {

            throw new DashSpecParseException("Layout board requires at least one row [ … ].");

        }



        return new LayoutBoardDefinition(rows);

    }



    public static PlacementDefinition ParsePlacement(TokenReader reader)

    {

        var props = PropertyBlockParser.Parse(reader, PropertySchemas.Placement, "place");



        var row = 1;

        var col = 1;

        var span = 6;



        if (props.TryGetValue("row", out var rowRaw) &&

            int.TryParse(rowRaw, out var parsedRow) &&

            parsedRow > 0)

        {

            row = parsedRow;

        }



        if (props.TryGetValue("col", out var colRaw) &&

            int.TryParse(colRaw, out var parsedCol) &&

            parsedCol > 0)

        {

            col = parsedCol;

        }



        if (props.TryGetValue("span", out var spanRaw))

        {

            span = ParseSpanValue(spanRaw);

        }



        return new PlacementDefinition(row, col, span);

    }



    private static IReadOnlyList<string> ParseBoardRow(TokenReader reader)

    {

        reader.Expect(TokenKind.LBracket);

        reader.SkipNewlines();



        var cells = new List<string>();



        while (!reader.IsAt(TokenKind.RBracket) && !reader.IsEof)

        {

            reader.SkipNewlines();

            if (reader.IsAt(TokenKind.RBracket))

            {

                break;

            }



            cells.Add(reader.ReadIdent());

            reader.SkipNewlines();

        }



        reader.Expect(TokenKind.RBracket);



        if (cells.Count == 0)

        {

            throw new DashSpecParseException("Layout board row [ … ] must list at least one card ref or id.");

        }



        return cells;

    }



    private static int ParseSpanValue(string value) =>

        value.ToLowerInvariant() switch

        {

            "full" => 12,

            "half" => 6,

            "third" => 4,

            _ when int.TryParse(value, out var parsed) && parsed > 0 => parsed,

            _ => 6,

        };

}


