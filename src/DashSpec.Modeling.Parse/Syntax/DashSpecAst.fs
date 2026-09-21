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

    let span (node: DashSpecAstNode) =
        match node with
        | DashSpecAstNode.CompilationUnit n -> n.Span
        | DashSpecAstNode.ModuleDeclaration n -> n.Span
        | DashSpecAstNode.BlockDeclaration n -> n.Span
        | DashSpecAstNode.CardReference n -> n.Span
        | DashSpecAstNode.EndBlock n -> n.Span
        | DashSpecAstNode.Line n -> n.Span
        | DashSpecAstNode.BlankLine n -> n.Span

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

    let isEndBlock (node: DashSpecAstNode) =
        match node with
        | DashSpecAstNode.EndBlock _ -> true
        | _ -> false

    let outlineLabel (node: DashSpecAstNode) =
        match node with
        | DashSpecAstNode.ModuleDeclaration n ->
            match n.Directive with
            | DashSpecModuleDirective.Dashboard id -> $"@dashboard {id}"
            | DashSpecModuleDirective.Tab id -> $"@tab {id}"
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

    let isOutlineNode (node: DashSpecAstNode) =
        match node with
        | DashSpecAstNode.ModuleDeclaration _
        | DashSpecAstNode.BlockDeclaration _
        | DashSpecAstNode.CardReference _ -> true
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
