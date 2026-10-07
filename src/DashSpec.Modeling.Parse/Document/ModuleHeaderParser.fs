namespace DashSpec.Modeling.Parse.Document

open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

/// <summary>Parses optional <c>namespace</c> and <c>import</c> lines at the start of @dashboard/@tab bodies (ADR-0098).</summary>
module ModuleHeaderParser =

    let private parseImportAfterKeyword (reader: TokenReader) =
        let kindKeyword = reader.ReadIdent()

        match ImportKindRegistry.tryFindByKeyword kindKeyword with
        | None -> raise (DashSpecParseException($"Unknown import kind '{kindKeyword}'."))
        | Some kind ->
            reader.ExpectKeyword "from"
            let namespaceName = AccessorGrammar.readQualifiedName reader
            let alias =
                if reader.TryKeyword "as" then Some(reader.ReadIdent()) else None

            { Kind = kind
              Namespace = namespaceName
              Alias = alias }

    let parse (reader: TokenReader) : ModuleHeader =
        let imports = ResizeArray<ModuleImportDirective>()
        let mutable namespaceName: string option = None
        let mutable continueHeader = true

        while continueHeader && not reader.IsEof do
            reader.SkipNewlines()

            if reader.TryKeyword "namespace" then
                if namespaceName.IsSome then
                    raise (DashSpecParseException("Module header allows only one 'namespace' directive."))

                namespaceName <- Some(AccessorGrammar.readQualifiedName reader)
            else
                let saved = reader.SavePosition()

                if reader.TryKeyword "import" then
                    if reader.IsAt TokenKind.String then
                        reader.RestorePosition saved
                        continueHeader <- false
                    else
                        imports.Add(parseImportAfterKeyword reader)
                else
                    continueHeader <- false

        { Namespace = namespaceName
          Imports = imports :> IReadOnlyList<_> }
