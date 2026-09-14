namespace DashSpec.Modeling.Parse.Toolbar

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Layout
open DashSpec.Modeling.Parse.Lexing

type ToolbarPlacementContext =
    { AssignToolbarBoard: LayoutBoardDefinition -> unit
      AddDashboardFilters: IReadOnlyList<string> -> unit }

module ToolbarPlacementParser =

    let private resolveEndKind (blockName: string) =
        let parts = blockName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
        if parts.Length >= 2
           && String.Equals(parts.[0], "layout", StringComparison.OrdinalIgnoreCase)
           && String.Equals(parts.[1], "board", StringComparison.OrdinalIgnoreCase) then
            "layout", Some "board"
        else
            parts.[parts.Length - 1], None

    let private parseNameListUntilEnd (reader: TokenReader) (endKind: string) (endId: string option) (blockName: string) =
        let names = ResizeArray<string>()
        while not (BlockSyntax.isBlockEnd reader endKind endId) && not reader.IsEof do
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader endKind endId then ()
            else
                names.Add(reader.ReadIdent())
                reader.SkipNewlines()
                if reader.CurrentKind = TokenKind.Comma then reader.Advance()
        reader.SkipNewlines()
        BlockSyntax.expectBlockEnd reader endKind endId
        if names.Count = 0 then
            raise (DashSpecParseException($"{blockName} block requires at least one name."))
        names :> IReadOnlyList<_>

    let private parseCore
        (reader: TokenReader)
        (blockName: string)
        (onBoard: LayoutBoardDefinition -> unit)
        (onFlatNames: IReadOnlyList<string> -> unit)
        =
        let endKind, endId = resolveEndKind blockName

        if reader.IsOnNewline() then
            reader.SkipNewlines()
            if reader.IsAt TokenKind.LBracket then
                BlockSyntax.beginBlock reader
                onBoard(LayoutParser.parseBoardRows reader (Some endKind) endId)
                BlockSyntax.expectBlockEnd reader endKind endId
            else
                BlockSyntax.beginBlock reader
                onFlatNames(parseNameListUntilEnd reader endKind endId blockName)
        elif reader.IsAt TokenKind.LBracket then
            onBoard(LayoutParser.parseBoardRows reader (Some endKind) endId)
        else
            onFlatNames(reader.ReadCommaListInline())

    let parse (reader: TokenReader) (ctx: ToolbarPlacementContext) (blockName: string) =
        parseCore reader blockName ctx.AssignToolbarBoard ctx.AddDashboardFilters

    let discard (reader: TokenReader) (blockName: string) =
        parseCore reader blockName (fun _ -> ()) (fun _ -> ())
