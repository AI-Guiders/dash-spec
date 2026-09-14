namespace DashSpec.Modeling.Parse.Layout

open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

module LayoutParser =

    let private parseBoardRow (reader: TokenReader) =
        reader.Expect TokenKind.LBracket
        reader.SkipNewlines()
        let cells = ResizeArray<string>()
        while not (reader.IsAt TokenKind.RBracket) && not reader.IsEof do
            reader.SkipNewlines()
            if reader.IsAt TokenKind.RBracket then ()
            else
                cells.Add(reader.ReadIdent())
                reader.SkipNewlines()
        reader.Expect TokenKind.RBracket
        if cells.Count = 0 then
            raise (DashSpec.Modeling.Core.DashSpecParseException("Layout board row [ … ] must list at least one card ref or id."))
        cells :> IReadOnlyList<string>

    /// Bracket rows until EOF or end kind (for .dashlayout modules).
    let parseBoardRows (reader: TokenReader) (endKind: string option) (endId: string option) =
        let rows = ResizeArray<IReadOnlyList<string>>()
        reader.SkipNewlines()
        while not reader.IsEof
              && not (reader.IsAt TokenKind.RBrace)
              && (endKind.IsNone || not (BlockSyntax.isBlockEnd reader endKind.Value endId)) do
            if not (reader.IsAt TokenKind.LBracket) then
                raise (reader.Unexpected("["))
            rows.Add(parseBoardRow reader)
            reader.SkipNewlines()
        if rows.Count = 0 then
            raise (DashSpec.Modeling.Core.DashSpecParseException("Layout board requires at least one row [ … ]."))
        { Rows = rows :> IReadOnlyList<_>; ModuleScope = None }