namespace DashSpec.Modeling.Parse.Syntax

open System

module DashSpecAst =

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

    let rec descendants (node: DashSpecAstNode) =
        seq {
            yield node

            for child in members node do
                yield! descendants child
        }
