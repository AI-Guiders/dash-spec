namespace DashSpec.Modeling.Parse

open DashSpec.Modeling.Parse.Lexing

module ParserUtilities =

    let createReader (text: string) = TokenReader(DashSpecLexer.tokenize text)

    /// Reads optional ref &lt;id&gt; postfix without crossing a newline.
    let tryReadLayoutRef (reader: TokenReader) =
        if reader.TryKeywordSameLine "ref" then
            Some(reader.ReadIdentSameLine())
        else
            None

    let parseFilterPlacementList (reader: TokenReader) (endKind: string) (blockName: string) =
        if reader.IsOnNewline() then
            reader.SkipNewlines()
            PropertyBlockParser.parseCommaListBlock reader endKind blockName
        else
            reader.ReadCommaListInline()