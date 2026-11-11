namespace DashSpec.Modeling.Parse.Syntax

open System
open DashSpec.Modeling.Parse.Lexing

module DashSpecAst =

    let id (node: DashSpecAstNode) =
        match node with
        | DashSpecAstNode.CompilationUnit n -> n.Id
        | DashSpecAstNode.ModuleDeclaration n -> n.Id
        | DashSpecAstNode.BlockDeclaration n -> n.Id
        | DashSpecAstNode.CardReference n -> n.Id
        | DashSpecAstNode.EndBlock n -> n.Id
        | DashSpecAstNode.Line n -> n.Id
        | DashSpecAstNode.BlankLine n -> n.Id
        | DashSpecAstNode.ImportDeclaration n -> n.Id
        | DashSpecAstNode.TypeFieldDeclaration n -> n.Id
        | DashSpecAstNode.PropertyAssignment n -> n.Id
        | DashSpecAstNode.PortDeclaration n -> n.Id
        | DashSpecAstNode.FlowLinkDeclaration n -> n.Id
        | DashSpecAstNode.ClauseDeclaration n -> n.Id
        | DashSpecAstNode.GridRowDeclaration n -> n.Id
        | DashSpecAstNode.TableRowDeclaration n -> n.Id

    let span (node: DashSpecAstNode) =
        match node with
        | DashSpecAstNode.CompilationUnit n -> n.Span
        | DashSpecAstNode.ModuleDeclaration n -> n.Span
        | DashSpecAstNode.BlockDeclaration n -> n.Span
        | DashSpecAstNode.CardReference n -> n.Span
        | DashSpecAstNode.EndBlock n -> n.Span
        | DashSpecAstNode.Line n -> n.Span
        | DashSpecAstNode.BlankLine n -> n.Span
        | DashSpecAstNode.ImportDeclaration n -> n.Span
        | DashSpecAstNode.TypeFieldDeclaration n -> n.Span
        | DashSpecAstNode.PropertyAssignment n -> n.Span
        | DashSpecAstNode.PortDeclaration n -> n.Span
        | DashSpecAstNode.FlowLinkDeclaration n -> n.Span
        | DashSpecAstNode.ClauseDeclaration n -> n.Span
        | DashSpecAstNode.GridRowDeclaration n -> n.Span
        | DashSpecAstNode.TableRowDeclaration n -> n.Span

    let members (node: DashSpecAstNode) =
        match node with
        | DashSpecAstNode.CompilationUnit n -> n.Members
        | DashSpecAstNode.ModuleDeclaration n -> n.Members
        | DashSpecAstNode.BlockDeclaration n -> n.Members
        | _ -> Array.empty

    let tokens (node: DashSpecAstNode) =
        match node with
        | DashSpecAstNode.ModuleDeclaration n -> n.Tokens
        | DashSpecAstNode.BlockDeclaration n -> n.Tokens
        | DashSpecAstNode.CardReference n -> n.Tokens
        | DashSpecAstNode.EndBlock n -> n.Tokens
        | DashSpecAstNode.Line n -> n.Tokens
        | DashSpecAstNode.ImportDeclaration n -> n.Tokens
        | DashSpecAstNode.TypeFieldDeclaration n -> n.Tokens
        | DashSpecAstNode.PropertyAssignment n -> n.Tokens
        | DashSpecAstNode.PortDeclaration n -> n.Tokens
        | DashSpecAstNode.FlowLinkDeclaration n -> n.Tokens
        | DashSpecAstNode.ClauseDeclaration n -> n.Tokens
        | DashSpecAstNode.GridRowDeclaration n -> n.Tokens
        | DashSpecAstNode.TableRowDeclaration n -> n.Tokens
        | _ -> Array.empty

    let withSpan (node: DashSpecAstNode) (newSpan: TextSpan) (newMembers: DashSpecAstNode[]) =
        match node with
        | DashSpecAstNode.CompilationUnit n -> DashSpecAstNode.CompilationUnit { n with Span = newSpan; Members = newMembers }
        | DashSpecAstNode.ModuleDeclaration n -> DashSpecAstNode.ModuleDeclaration { n with Span = newSpan; Members = newMembers }
        | DashSpecAstNode.BlockDeclaration n -> DashSpecAstNode.BlockDeclaration { n with Span = newSpan; Members = newMembers }
        | _ -> node

    let nodeKind (node: DashSpecAstNode) =
        match node with
        | DashSpecAstNode.CompilationUnit _ -> DashSpecAstNodeKind.CompilationUnit
        | DashSpecAstNode.ModuleDeclaration _ -> DashSpecAstNodeKind.ModuleDeclaration
        | DashSpecAstNode.BlockDeclaration _ -> DashSpecAstNodeKind.BlockDeclaration
        | DashSpecAstNode.CardReference _ -> DashSpecAstNodeKind.CardReference
        | DashSpecAstNode.EndBlock _ -> DashSpecAstNodeKind.EndBlock
        | DashSpecAstNode.Line _ -> DashSpecAstNodeKind.Line
        | DashSpecAstNode.BlankLine _ -> DashSpecAstNodeKind.BlankLine
        | DashSpecAstNode.ImportDeclaration _ -> DashSpecAstNodeKind.ImportDeclaration
        | DashSpecAstNode.TypeFieldDeclaration _ -> DashSpecAstNodeKind.TypeFieldDeclaration
        | DashSpecAstNode.PropertyAssignment _ -> DashSpecAstNodeKind.PropertyAssignment
        | DashSpecAstNode.PortDeclaration _ -> DashSpecAstNodeKind.PortDeclaration
        | DashSpecAstNode.FlowLinkDeclaration _ -> DashSpecAstNodeKind.FlowLinkDeclaration
        | DashSpecAstNode.ClauseDeclaration _ -> DashSpecAstNodeKind.ClauseDeclaration
        | DashSpecAstNode.GridRowDeclaration _ -> DashSpecAstNodeKind.GridRowDeclaration
        | DashSpecAstNode.TableRowDeclaration _ -> DashSpecAstNodeKind.TableRowDeclaration

    let isEndBlock (node: DashSpecAstNode) =
        match node with
        | DashSpecAstNode.EndBlock _ -> true
        | _ -> false

    let outlineLabel (node: DashSpecAstNode) =
        match node with
        | DashSpecAstNode.ModuleDeclaration n ->
            match n.Directive with
            | DashSpecModuleDirective.Dashboard id -> $"module dashboard {id}"
            | DashSpecModuleDirective.Tab id -> $"module tab {id}"
            | DashSpecModuleDirective.Module(kind, id) -> $"module {DashSpecModuleKind.toKeyword kind} {id}"
        | DashSpecAstNode.BlockDeclaration n ->
            match n.Opener with
            | DashSpecBlockOpener.Named(keyword, identifier) ->
                $"{DashSpecBlockKeyword.toEndName keyword} {identifier}"
            | DashSpecBlockOpener.Anonymous keyword -> DashSpecBlockKeyword.toEndName keyword
        | DashSpecAstNode.CardReference n -> $"card {n.CardId}"
        | DashSpecAstNode.EndBlock n ->
            match n.EndId with
            | Some id -> $"end {DashSpecBlockKeyword.toEndName n.EndKeyword} {id}"
            | None -> $"end {DashSpecBlockKeyword.toEndName n.EndKeyword}"
        | _ ->
            String.Join(" ", tokens node |> Array.map (fun token -> token.Text)).Trim()

    /// Trivia: blank lines and comment-only lines. Grammar, concepts and rules ignore them.
    let isTrivia (node: DashSpecAstNode) =
        match node with
        | DashSpecAstNode.BlankLine _ -> true
        | DashSpecAstNode.Line n ->
            not (Array.isEmpty n.Tokens)
            && n.Tokens
               |> Array.forall (fun token -> token.LexKind = TokenKind.LineComment || token.LexKind = TokenKind.BlockComment)
        | _ -> false

    let isOutlineNode (node: DashSpecAstNode) =
        match node with
        | DashSpecAstNode.ModuleDeclaration _
        | DashSpecAstNode.BlockDeclaration _
        | DashSpecAstNode.CardReference _ -> true
        | _ -> false

    /// Semantic concept-bearing AST node (containers + typed member declarations).
    let isConceptNode (node: DashSpecAstNode) =
        match node with
        | DashSpecAstNode.ModuleDeclaration _
        | DashSpecAstNode.BlockDeclaration _
        | DashSpecAstNode.CardReference _
        | DashSpecAstNode.ImportDeclaration _
        | DashSpecAstNode.TypeFieldDeclaration _
        | DashSpecAstNode.PropertyAssignment _
        | DashSpecAstNode.PortDeclaration _
        | DashSpecAstNode.FlowLinkDeclaration _
        | DashSpecAstNode.ClauseDeclaration _
        | DashSpecAstNode.GridRowDeclaration _
        | DashSpecAstNode.TableRowDeclaration _ -> true
        | _ -> false

    let private unwrapStringLiteral (text: string) =
        if text.Length >= 2 && text.[0] = '"' && text.[text.Length - 1] = '"' then
            text.Substring(1, text.Length - 2)
        else
            text

    /// Display title from `as "…"` on the opener/header line, when present.
    let tryTitle (node: DashSpecAstNode) =
        let tokens = tokens node

        tokens
        |> Array.tryFindIndex (fun token -> token.Text = "as")
        |> Option.bind (fun index ->
            if index + 1 < tokens.Length then
                let next = tokens.[index + 1]

                if next.LexKind = TokenKind.String then
                    Some(unwrapStringLiteral next.Text)
                else
                    None
            else
                None)

    let rec hasClosingEndBlock (node: DashSpecAstNode) (keyword: DashSpecBlockKeyword) (identifier: string option) =
        match node with
        | DashSpecAstNode.EndBlock n when n.EndKeyword = keyword ->
            match identifier, n.EndId with
            | Some openerId, Some endId -> String.Equals(openerId, endId, StringComparison.Ordinal)
            | Some _, None -> true
            | None, Some _ -> false
            | None, None -> true
        | _ -> members node |> Array.exists (fun child -> hasClosingEndBlock child keyword identifier)

    let rec descendants (node: DashSpecAstNode) =
        seq {
            yield node

            for child in members node do
                yield! descendants child
        }
