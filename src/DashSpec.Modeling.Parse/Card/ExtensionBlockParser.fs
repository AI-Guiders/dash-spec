namespace DashSpec.Modeling.Parse.Card

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

module ExtensionBlockParser =

    let rec private parseBlock (reader: TokenReader) (keyword: string) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let properties = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        let nested = ResizeArray<ExtensionBlockNode>()

        while not (BlockSyntax.isBlockEnd reader keyword None) && not reader.IsEof do
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader keyword None then ()
            elif reader.CurrentKind = TokenKind.TableRow then
                let tableRows = ResizeArray<string[]>()
                while not reader.IsEof && reader.CurrentKind = TokenKind.TableRow do
                    tableRows.Add(TableLiteral.splitCells (reader.CurrentTableRow()))
                    reader.Advance()
                    reader.SkipNewlines()

                if tableRows.Count > 1 then
                    let headers = tableRows.[0] |> Array.map (fun h -> h.Trim())
                    let idColIdx =
                        headers
                        |> Array.tryFindIndex (fun h -> String.Equals(h, "id", StringComparison.OrdinalIgnoreCase))
                        |> Option.defaultValue 0

                    for r in tableRows |> Seq.skip 1 do
                        if not (TableLiteral.isSeparator r) && idColIdx < r.Length then
                            let rowId = r.[idColIdx].Trim().Trim('"')
                            if not (String.IsNullOrWhiteSpace rowId) && rowId <> "-" then
                                let rowProps = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                                for i in 0 .. (min (r.Length - 1) (headers.Length - 1)) do
                                    if i <> idColIdx then
                                        let colName = headers.[i]
                                        let cellVal = r.[i].Trim().Trim('"')
                                        if not (String.IsNullOrWhiteSpace colName) && not (String.IsNullOrWhiteSpace cellVal) && cellVal <> "-" then
                                            rowProps.[colName] <- cellVal
                                nested.Add
                                    { Keyword = rowId
                                      Properties = rowProps :> IReadOnlyDictionary<_, _>
                                      Nested = [||] }
            else
                let mark = reader.SavePosition()
                let first = reader.ReadIdent()
                reader.SkipNewlines()

                if reader.IsAt TokenKind.Eq then
                    reader.Expect TokenKind.Eq
                    properties.[first] <- reader.ReadScalarValue()
                    reader.SkipNewlines()
                elif not (BlockSyntax.isBlockEnd reader keyword None) then
                    reader.RestorePosition mark
                    let childKeyword = reader.ReadIdent()
                    nested.Add(parseBlock reader childKeyword)
                    reader.SkipNewlines()
                else
                    let tail = reader.ReadRestOfLine()
                    properties.[first] <- if String.IsNullOrWhiteSpace tail then first else $"{first} {tail}".Trim()
                    reader.SkipNewlines()

        BlockSyntax.expectBlockEnd reader keyword None
        { Keyword = keyword; Properties = properties :> IReadOnlyDictionary<_, _>; Nested = nested :> IReadOnlyList<_> }

    let parse (reader: TokenReader) (keyword: string) (allowedTopLevelKeywords: IReadOnlySet<string>) =
        if not (allowedTopLevelKeywords.Contains keyword) then
            raise (DashSpecParseException($"Unknown extension block '{keyword}'."))

        let actual = reader.ReadIdent()
        if not (String.Equals(actual, keyword, StringComparison.OrdinalIgnoreCase)) then
            raise (DashSpecParseException($"Expected extension block '{keyword}', got '{actual}'."))

        parseBlock reader keyword
