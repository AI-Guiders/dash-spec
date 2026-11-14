namespace DashSpec.Modeling.Parse.Document

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

/// <summary>Parses optional <c>namespace</c> and <c>import</c> / <c>from &lt;Namespace&gt; import { … }</c> directives at the start of module bodies (ADR-0098).</summary>
module ModuleHeaderParser =

    let private resolveImportKind (kindKeyword: string) (sourceOffset: int) =
        match ImportKindRegistry.tryFindByKeyword kindKeyword with
        | None -> raise (DashSpecParseException($"Unknown import kind '{kindKeyword}'.", sourceOffset))
        | Some kind -> kind

    let private readOptionalImportAlias (reader: TokenReader) =
        if reader.IsAt TokenKind.Eq then
            reader.Advance()
            Some(reader.ReadIdent())
        elif reader.TryKeyword "as" then
            Some(reader.ReadIdent())
        else
            None

    /// <summary>Parses <c>from [alias =] &lt;Namespace&gt; import { kind [= alias] … }</c>, <c>from &lt;Namespace&gt; import … end import</c>,
    /// or single-line <c>from [alias =] &lt;Namespace&gt; import kind [= alias]</c>.</summary>
    let private parseFromNamespaceImport (reader: TokenReader) (imports: ResizeArray<ModuleImportDirective>) =
        let sourceOffset = reader.CurrentOffset
        let firstName = AccessorGrammar.readQualifiedName reader
        let moduleAlias, namespaceName =
            if reader.IsAt TokenKind.Eq then
                reader.Advance()
                Some firstName, AccessorGrammar.readQualifiedName reader
            else
                None, firstName

        reader.ExpectKeyword "import"

        let readOneItem (itemOffset: int) =
            let kindKeyword = reader.ReadIdent()
            let kind = resolveImportKind kindKeyword itemOffset
            let itemAlias = readOptionalImportAlias reader |> Option.orElse moduleAlias
            if reader.IsAt TokenKind.Comma then
                reader.Advance()
            imports.Add(
                { Kind = kind
                  Namespace = namespaceName
                  Alias = itemAlias
                  SourceOffset = itemOffset }
            )

        if reader.IsAt TokenKind.LBrace || reader.IsOnNewline() then
            BlockSyntax.beginBlock reader
            reader.SkipNewlines()
            while not (BlockSyntax.isBlockEnd reader "import" None) && not reader.IsEof do
                reader.SkipNewlines()
                if not (BlockSyntax.isBlockEnd reader "import" None) then
                    let itemOffset = reader.CurrentOffset
                    readOneItem itemOffset
                    reader.SkipNewlines()
            BlockSyntax.expectBlockEnd reader "import" None
        else
            readOneItem sourceOffset
            while reader.IsAt TokenKind.Comma do
                reader.Advance()
                readOneItem reader.CurrentOffset

    /// <summary>Parses either <c>import &lt;kind&gt; from &lt;Namespace&gt; [= alias]</c>
    /// or whole-module alias <c>import &lt;alias&gt; = &lt;Namespace&gt;</c> (importing all kinds present in that namespace).</summary>
    let private parseImportStatement (reader: TokenReader) (imports: ResizeArray<ModuleImportDirective>) =
        let sourceOffset = reader.CurrentOffset
        let firstIdent = reader.ReadIdent()

        if reader.IsAt TokenKind.Eq && (ImportKindRegistry.tryFindByKeyword firstIdent |> Option.isNone) then
            // Whole-module alias: `import stk = Demo.Stakeholder`
            reader.Advance()
            let namespaceName = AccessorGrammar.readQualifiedName reader
            for def in ImportKindRegistry.allDefinitions do
                imports.Add(
                    { Kind = def.Kind
                      Namespace = namespaceName
                      Alias = Some firstIdent
                      SourceOffset = sourceOffset }
                )
        else
            let kind = resolveImportKind firstIdent sourceOffset
            let preAlias =
                if reader.IsAt TokenKind.Eq then
                    reader.Advance()
                    Some(reader.ReadIdent())
                else
                    None

            reader.ExpectKeyword "from"
            let namespaceName = AccessorGrammar.readQualifiedName reader
            let postAlias = readOptionalImportAlias reader

            imports.Add(
                { Kind = kind
                  Namespace = namespaceName
                  Alias = postAlias |> Option.orElse preAlias
                  SourceOffset = sourceOffset }
            )

    let parse (reader: TokenReader) : ModuleHeader =
        let imports = ResizeArray<ModuleImportDirective>()
        let mutable namespaceName: string option = None
        let mutable continueHeader = true

        while continueHeader && not reader.IsEof do
            reader.SkipNewlines()
            let directiveOffset = reader.CurrentOffset

            if reader.TryKeyword "namespace" then
                if namespaceName.IsSome then
                    raise (DashSpecParseException("Module header allows only one 'namespace' directive.", directiveOffset))

                namespaceName <- Some(AccessorGrammar.readQualifiedName reader)
            else
                let saved = reader.SavePosition()

                if reader.TryKeyword "from" then
                    // Distinguish header `from [alias =] <Namespace> import ...` from dataflow body `from view|sql|xlsx ...`
                    let _first = AccessorGrammar.readQualifiedName reader
                    if reader.IsAt TokenKind.Eq then
                        reader.Advance()
                        AccessorGrammar.readQualifiedName reader |> ignore
                    if reader.TryKeyword "import" then
                        reader.RestorePosition saved
                        reader.ExpectKeyword "from"
                        parseFromNamespaceImport reader imports
                    else
                        reader.RestorePosition saved
                        continueHeader <- false
                elif reader.TryKeyword "import" then
                    if reader.IsAt TokenKind.String then
                        reader.RestorePosition saved
                        continueHeader <- false
                    else
                        parseImportStatement reader imports
                else
                    continueHeader <- false

        { Namespace = namespaceName
          Imports = imports :> IReadOnlyList<_> }
