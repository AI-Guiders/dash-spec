namespace DashSpec.Modeling.Parse.Toolbar

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

module CommandAliasesParser =

    let parse (reader: TokenReader) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let map = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

        while not (BlockSyntax.isBlockEnd reader "commands" None) && not reader.IsEof do
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader "commands" None then ()
            else
                let alias = reader.ReadIdent()
                if String.IsNullOrWhiteSpace alias then
                    raise (DashSpecParseException("commands entry requires an alias name."))
                reader.Expect TokenKind.Eq
                let filterId = reader.ReadIdent()
                if String.IsNullOrWhiteSpace filterId then
                    raise (DashSpecParseException($"commands alias '{alias}' requires a filter id."))
                if not (map.TryAdd(alias, filterId)) then
                    raise (DashSpecParseException($"commands declares duplicate alias '{alias}'."))
                reader.SkipNewlines()

        BlockSyntax.expectBlockEnd reader "commands" None
        map :> IReadOnlyDictionary<_, _>
