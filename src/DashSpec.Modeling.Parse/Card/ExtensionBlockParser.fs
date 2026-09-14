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
