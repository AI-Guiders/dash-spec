namespace DashSpec.Modeling.Parse.Display

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
        reader.SkipNewlines()
        let blockName = $"bind display in '{scopeId}'"
        let bindings = MemberGrammar.parseStringMapBody reader "bind" blockName readSourcePath
        BlockSyntax.expectBlockEnd reader "bind" None
        bindings :> IReadOnlyDictionary<string, string>
