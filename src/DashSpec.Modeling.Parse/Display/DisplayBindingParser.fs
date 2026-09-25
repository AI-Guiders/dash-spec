namespace DashSpec.Modeling.Parse.Display

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

module DisplayBindingParser =

    let private readSourcePath (reader: TokenReader) =
        let filterName = reader.ReadIdent()
        if reader.CurrentKind = TokenKind.Dot then
            reader.Advance()
            let property = reader.ReadIdent()
            $"{filterName}.{property}"
        else
            filterName

    let parse (reader: TokenReader) (scopeId: string) =
        let bindings =
            Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

        reader.SkipNewlines()

        while not (BlockSyntax.isBlockEnd reader "bind" None) && not reader.IsEof do
            reader.SkipNewlines()

            if BlockSyntax.isBlockEnd reader "bind" None then ()
            else
                let slot = reader.ReadIdent()

                if String.IsNullOrWhiteSpace slot then
                    raise (DashSpecParseException($"bind display in '{scopeId}': slot name is required."))

                reader.Expect TokenKind.Eq
                let source = readSourcePath reader

                if bindings.ContainsKey slot then
                    raise (DashSpecParseException($"bind display in '{scopeId}': duplicate slot '{slot}'."))

                bindings.[slot] <- source
                reader.SkipNewlines()

        BlockSyntax.expectBlockEnd reader "bind" None
        bindings :> IReadOnlyDictionary<string, string>
