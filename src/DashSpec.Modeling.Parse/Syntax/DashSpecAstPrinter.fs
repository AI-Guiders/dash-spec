namespace DashSpec.Modeling.Parse.Syntax

open System
open AIGuiders.Platform.Modeling.Core.Identity
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Lexing

/// <summary>ADR-0128: Pure AST synthesis, AST-level member merge, and AST → text projection
/// for types-units. The compiler works exclusively on <see cref="DashSpecAstNode"/>;
/// text exists only at the final serialization boundary.</summary>
module DashSpecAstPrinter =

    let private emptySpan = TextSpan.Create 0 0

    let private mkToken (kind: DashSpecSyntaxKind) (text: string) : SyntaxToken =
        { Kind = kind
          LexKind = TokenKind.Ident
          Text = text
          Span = emptySpan }

    let private keyword text = mkToken DashSpecSyntaxKind.Keyword text
    let private ident text = mkToken DashSpecSyntaxKind.Identifier text

    let private fieldTokens (field: RowFieldDef) : SyntaxToken[] =
        let typeTokens =
            match field.Type with
            | DashType.Primitive primitive -> [| ident (DashPrimitive.toString primitive) |]
            | DashType.Named name -> [| ident name |]
            | DashType.FixedArray (element, length) ->
                [| keyword "array"
                   ident (DashPrimitive.toString element)
                   ident (string length) |]

        let prefix =
            if field.Optional then
                [| keyword "optional" |]
            else
                [||]

        Array.concat [ prefix; typeTokens; [| ident field.Name |] ]

    let private synthesizeFieldNode (nextId: unit -> NodeId) (field: RowFieldDef) : DashSpecAstNode =
        DashSpecAstNode.TypeFieldDeclaration
            { Id = nextId ()
              Span = emptySpan
              Optional = field.Optional
              FieldType = field.Type
              FieldPath = DotSyntax.parse field.Name
              FieldName = field.Name
              Tokens = fieldTokens field }

    let private synthesizeEndType (nextId: unit -> NodeId) : DashSpecAstNode =
        DashSpecAstNode.EndBlock
            { Id = nextId ()
              Span = emptySpan
              EndKeyword = DashSpecBlockKeyword.Type
              EndId = None
              Tokens = [| keyword "end"; keyword "type" |] }

    /// Synthesize one `type <Name> … end type` AST block node from IR.
    let synthesizeTypeBlock (nextId: unit -> NodeId) (def: RowTypeDef) : DashSpecAstNode =
        let fieldNodes = def.Fields |> Array.map (synthesizeFieldNode nextId)
        let endNode = synthesizeEndType nextId

        DashSpecAstNode.BlockDeclaration
            { Id = nextId ()
              Span = emptySpan
              Opener = DashSpecBlockOpener.Named(DashSpecBlockKeyword.Type, def.Name)
              TypeAnnotation = None
              Origin = None
              HeaderSpan = emptySpan
              Tokens = [| keyword "type"; ident def.Name |]
              Members = Array.append fieldNodes [| endNode |] }

    let private blankLine (nextId: unit -> NodeId) : DashSpecAstNode =
        DashSpecAstNode.BlankLine { Id = nextId (); Span = emptySpan }

    /// Synthesize the AST of a types-unit (`module types <id>` with optional `namespace <ns>`
    /// and one `BlockDeclaration` per row type) without any intermediate string construction.
    let synthesizeTypesUnit (moduleId: string) (ns: string option) (definitions: RowTypeDef seq) : DashSpecAstNode =
        let mutable counter = 0L

        let nextId () =
            counter <- counter + 1L
            NodeId.mint (NumericId.ofCounter counter)

        let preamble =
            match ns with
            | Some nsValue when not (String.IsNullOrWhiteSpace nsValue) ->
                let trimmed = nsValue.Trim()
                [| DashSpecAstNode.ClauseDeclaration
                       { Id = nextId ()
                         Span = emptySpan
                         Kind = ClauseKind.NamespaceClause trimmed
                         Tokens = [| keyword "namespace"; ident trimmed |] }
                   blankLine nextId |]
            | _ -> [| blankLine nextId |]

        let defs = Array.ofSeq definitions

        let blocksWithSpacing =
            defs
            |> Array.mapi (fun index def ->
                let block = synthesizeTypeBlock nextId def

                if index < defs.Length - 1 then
                    [| block; blankLine nextId |]
                else
                    [| block |])
            |> Array.concat

        DashSpecAstNode.CompilationUnit
            { Id = nextId ()
              Span = emptySpan
              Members =
                [| DashSpecAstNode.ModuleDeclaration
                       { Id = nextId ()
                         Span = emptySpan
                         Directive = DashSpecModuleDirective.Module(DashSpecModuleKind.Types, moduleId)
                         HeaderSpan = emptySpan
                         Tokens = [| keyword "module"; keyword "types"; ident moduleId |]
                         Members = Array.append preamble blocksWithSpacing } |] }

    let private maxNodeId (root: DashSpecAstNode) : int64 =
        let mutable maxVal = NumericId.value (NodeId.carrier (DashSpecAst.id root))

        for node in DashSpecAst.descendants root do
            let v = NumericId.value (NodeId.carrier (DashSpecAst.id node))
            if v > maxVal then maxVal <- v

        maxVal

    let private isTypeBlockNamed (expectedName: string) (node: DashSpecAstNode) : bool =
        match node with
        | DashSpecAstNode.BlockDeclaration block ->
            match block.Opener with
            | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Type, name) ->
                String.Equals(name, expectedName, StringComparison.OrdinalIgnoreCase)
            | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Other kw, name) ->
                String.Equals(kw, "type", StringComparison.OrdinalIgnoreCase)
                && String.Equals(name, expectedName, StringComparison.OrdinalIgnoreCase)
            | _ -> false
        | _ -> false

    let private hasAnyTypeBlock (members: DashSpecAstNode[]) : bool =
        members
        |> Array.exists (function
            | DashSpecAstNode.BlockDeclaration _ -> true
            | _ -> false)

    /// Mutate an in-memory CompilationUnit AST by merging a RowTypeDef:
    /// if `type <Name>` exists, only its field nodes are updated in-place (preserving
    /// surrounding blocks, order, and headers); otherwise a new BlockDeclaration is appended.
    let mergeTypeIntoUnit (unit: DashSpecAstNode) (candidate: RowTypeDef) : DashSpecAstNode =
        let mutable counter = maxNodeId unit

        let nextId () =
            counter <- counter + 1L
            NodeId.mint (NumericId.ofCounter counter)

        match unit with
        | DashSpecAstNode.CompilationUnit cu ->
            let updatedCuMembers =
                cu.Members
                |> Array.map (function
                    | DashSpecAstNode.ModuleDeclaration md ->
                        let exists = md.Members |> Array.exists (isTypeBlockNamed candidate.Name)

                        let updatedMembers =
                            if exists then
                                md.Members
                                |> Array.map (fun memberNode ->
                                    if isTypeBlockNamed candidate.Name memberNode then
                                        match memberNode with
                                        | DashSpecAstNode.BlockDeclaration block ->
                                            let existingEnd =
                                                block.Members
                                                |> Array.tryFind DashSpecAst.isEndBlock
                                                |> Option.defaultWith (fun () -> synthesizeEndType nextId)

                                            let newFields = candidate.Fields |> Array.map (synthesizeFieldNode nextId)

                                            DashSpecAstNode.BlockDeclaration
                                                { block with
                                                    Members = Array.append newFields [| existingEnd |] }
                                        | other -> other
                                    else
                                        memberNode)
                            else
                                let newBlock = synthesizeTypeBlock nextId candidate

                                if hasAnyTypeBlock md.Members then
                                    Array.append md.Members [| blankLine nextId; newBlock |]
                                else
                                    Array.append md.Members [| newBlock |]

                        DashSpecAstNode.ModuleDeclaration { md with Members = updatedMembers }
                    | other -> other)

            DashSpecAstNode.CompilationUnit { cu with Members = updatedCuMembers }
        | other -> other

    /// Project a CompilationUnit AST subtree to text. All directives (including `namespace`)
    /// live as AST nodes inside the tree; formatting/indentation is orthogonal (`dsc format`).
    let print (unit: DashSpecAstNode) : string =
        match unit with
        | DashSpecAstNode.CompilationUnit compilationUnit ->
            let builder = Text.StringBuilder()

            let lineText (tokens: SyntaxToken[]) =
                let sb = Text.StringBuilder()

                for i = 0 to tokens.Length - 1 do
                    let cur = tokens.[i].Text

                    if i > 0 then
                        let prev = tokens.[i - 1].Text

                        if prev <> "." && cur <> "." then
                            sb.Append(' ') |> ignore

                    sb.Append(cur) |> ignore

                sb.ToString()

            let rec render (node: DashSpecAstNode) =
                match node with
                | DashSpecAstNode.ModuleDeclaration moduleDeclaration ->
                    builder.AppendLine(lineText moduleDeclaration.Tokens) |> ignore
                    moduleDeclaration.Members |> Array.iter render

                | DashSpecAstNode.ImportDeclaration importDeclaration ->
                    builder.AppendLine(lineText importDeclaration.Tokens) |> ignore

                | DashSpecAstNode.BlockDeclaration blockDeclaration ->
                    builder.AppendLine(lineText blockDeclaration.Tokens) |> ignore
                    blockDeclaration.Members |> Array.iter render

                | DashSpecAstNode.CardReference cardRef ->
                    builder.AppendLine(lineText cardRef.Tokens) |> ignore

                | DashSpecAstNode.TypeFieldDeclaration fieldDecl ->
                    builder.AppendLine(lineText fieldDecl.Tokens) |> ignore

                | DashSpecAstNode.PropertyAssignment propAssign ->
                    builder.AppendLine(lineText propAssign.Tokens) |> ignore

                | DashSpecAstNode.PortDeclaration portDecl ->
                    builder.AppendLine(lineText portDecl.Tokens) |> ignore

                | DashSpecAstNode.FlowLinkDeclaration flowLink ->
                    builder.AppendLine(lineText flowLink.Tokens) |> ignore

                | DashSpecAstNode.ClauseDeclaration clause ->
                    match clause.Kind with
                    | ClauseKind.NamespaceClause ns ->
                        builder.AppendLine($"namespace {ns}") |> ignore
                    | _ ->
                        builder.AppendLine(lineText clause.Tokens) |> ignore

                | DashSpecAstNode.GridRowDeclaration gridRow ->
                    builder.AppendLine(lineText gridRow.Tokens) |> ignore

                | DashSpecAstNode.TableRowDeclaration tableRow ->
                    builder.AppendLine(lineText tableRow.Tokens) |> ignore

                | DashSpecAstNode.Line line ->
                    builder.AppendLine(lineText line.Tokens) |> ignore

                | DashSpecAstNode.BlankLine _ ->
                    builder.AppendLine() |> ignore

                | DashSpecAstNode.EndBlock endBlock ->
                    builder.AppendLine(lineText endBlock.Tokens) |> ignore

                | DashSpecAstNode.CompilationUnit nested ->
                    nested.Members |> Array.iter render

            compilationUnit.Members |> Array.iter render
            builder.ToString()
        | _ -> invalidArg "unit" "print expects a CompilationUnit subtree."

    /// Compatibility alias over `print` (namespace is already carried inside the AST).
    let printTypesUnit (unit: DashSpecAstNode) (_ns: string option) : string = print unit

    /// The round-trip law (ADR-0128 §2): printed text reparses to the same type
    /// definitions — structural equality on every member. Broken law = printer bug.
    let roundTripHolds (moduleId: string) (definitions: RowTypeDef array) : bool =
        let printed = print (synthesizeTypesUnit moduleId None definitions)
        let reparsed = DashSpec.Modeling.Parse.Types.TypeModuleParser.parseTypesModule printed
        let reparsedByName = reparsed |> Seq.map (fun d -> d.Name, d) |> Map.ofSeq

        definitions
        |> Array.forall (fun def ->
            match Map.tryFind def.Name reparsedByName with
            | Some again -> TypeCatalog.rowTypesStructurallyEqual def again
            | None -> false)
