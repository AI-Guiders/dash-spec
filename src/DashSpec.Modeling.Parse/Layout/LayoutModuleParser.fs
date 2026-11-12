namespace DashSpec.Modeling.Parse.Layout

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Syntax

/// <summary>F# AST-first SSOT for <c>module layout &lt;id&gt;</c> units (ADR-0048 / ADR-0130).</summary>
module LayoutModuleParser =

    let private mapScope (scope: LayoutScopeSyntax) =
        match scope with
        | LayoutScopeSyntax.Toolbar -> LayoutScope.Toolbar
        | LayoutScopeSyntax.Section -> LayoutScope.Tab
        | LayoutScopeSyntax.Page -> LayoutScope.Page
        | LayoutScopeSyntax.Card -> LayoutScope.Card
        | LayoutScopeSyntax.Host -> LayoutScope.Host

    let private unwrapQuotes (v: string) =
        let trimmed = v.Trim()
        if trimmed.Length >= 2 && trimmed.[0] = '"' && trimmed.[trimmed.Length - 1] = '"' then
            trimmed.Substring(1, trimmed.Length - 2)
        else
            trimmed

    let private projectBoardEntriesFromAst (members: DashSpecAstNode[]) : LayoutBoardEntry list =
        let entries = ResizeArray<LayoutBoardEntry>()

        for memberNode in members do
            match memberNode with
            | DashSpecAstNode.GridRowDeclaration row ->
                if row.TypedCells |> Array.forall (fun c -> c.Target = "[]" && c.Weight.IsNone)
                   && not (row.Tokens |> Array.exists (fun t -> t.LexKind = DashSpec.Modeling.Parse.Lexing.TokenKind.LBracket)) then
                    raise (DashSpecParseException("Bare [] grid cells belong inside 'grid … end grid'; board rows require '[ … ]'."))
                entries.Add(CardRow(row.Cells :> IReadOnlyList<string>))
            | DashSpecAstNode.BlockDeclaration block ->
                match block.Opener with
                | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Group, groupId) ->
                    let mutable title: string option = None
                    let rows = ResizeArray<IReadOnlyList<string>>()

                    for child in block.Members do
                        match child with
                        | DashSpecAstNode.PropertyAssignment p when String.Equals(p.Key, "title", StringComparison.OrdinalIgnoreCase) ->
                            title <- Some(unwrapQuotes p.ValueText)
                        | DashSpecAstNode.PropertyAssignment p ->
                            raise (DashSpecParseException($"Unknown property '{p.Key}' in layout group '{groupId}' block."))
                        | DashSpecAstNode.GridRowDeclaration r ->
                            rows.Add(r.Cells :> IReadOnlyList<string>)
                        | DashSpecAstNode.BlankLine _
                        | DashSpecAstNode.EndBlock _ -> ()
                        | c when DashSpecAst.isTrivia c -> ()
                        | other ->
                            let lineText = DashSpecAst.tokens other |> Array.map (fun t -> t.Text) |> String.concat " "
                            raise (DashSpecParseException($"Unexpected statement '{lineText}' in layout group '{groupId}'."))

                    if rows.Count = 0 then
                        raise (DashSpecParseException($"Layout group '{groupId}' requires at least one row [ … ]."))

                    entries.Add(
                        GroupRow
                            { Id = groupId
                              Title = title
                              Rows = rows :> IReadOnlyList<IReadOnlyList<string>> })
                | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Nest, nestId) ->
                    let rows = ResizeArray<IReadOnlyList<string>>()

                    for child in block.Members do
                        match child with
                        | DashSpecAstNode.GridRowDeclaration r ->
                            rows.Add(r.Cells :> IReadOnlyList<string>)
                        | DashSpecAstNode.BlankLine _
                        | DashSpecAstNode.EndBlock _ -> ()
                        | c when DashSpecAst.isTrivia c -> ()
                        | other ->
                            let lineText = DashSpecAst.tokens other |> Array.map (fun t -> t.Text) |> String.concat " "
                            raise (DashSpecParseException($"Unexpected statement '{lineText}' in layout nest '{nestId}'."))

                    if rows.Count = 0 then
                        raise (DashSpecParseException($"Layout nest '{nestId}' requires at least one row [ … ]."))

                    entries.Add(NestRow { Id = nestId; Rows = rows :> IReadOnlyList<IReadOnlyList<string>> })
                | _ ->
                    let headerText = block.Tokens |> Array.map (fun t -> t.Text) |> String.concat " "
                    raise (DashSpecParseException($"Unexpected block '{headerText}' in layout module."))
            | DashSpecAstNode.ClauseDeclaration c ->
                match c.Kind with
                | ClauseKind.ScopeClause _ -> ()
                | _ ->
                    let lineText = c.Tokens |> Array.map (fun t -> t.Text) |> String.concat " "
                    raise (DashSpecParseException($"Unexpected clause '{lineText}' in layout module."))
            | other ->
                let toks = DashSpecAst.tokens other
                if toks.Length >= 2 && String.Equals(toks.[0].Text, "scope", StringComparison.OrdinalIgnoreCase) then
                    raise (DashSpecParseException($"Layout module scope must be toolbar, tab, page, card, or host; got '{toks.[1].Text}'."))
                let lineText = toks |> Array.map (fun t -> t.Text) |> String.concat " "
                raise (DashSpecParseException($"Unexpected statement '{lineText}' in layout module."))

        List.ofSeq entries

    /// <summary>Parse a <c>module layout &lt;id&gt;</c> file body directly from the typed syntax tree.</summary>
    let parseLayoutFile (text: string) : LayoutBoardDefinition =
        if String.IsNullOrWhiteSpace text then
            invalidArg "text" "Layout text is required."

        let tree = DashSpecSurfaceSyntax.parse text
        let env = UnitGrammar.unwrap DashSpecModuleKind.Layout tree.Root

        let scope =
            env.Body
            |> Array.tryPick (function
                | DashSpecAstNode.ClauseDeclaration c ->
                    match c.Kind with
                    | ClauseKind.ScopeClause s -> Some(mapScope s)
                    | _ -> None
                | DashSpecAstNode.Line l when l.Tokens.Length >= 2 && String.Equals(l.Tokens.[0].Text, "scope", StringComparison.OrdinalIgnoreCase) ->
                    raise (DashSpecParseException($"Layout module scope must be toolbar, tab, page, card, or host; got '{l.Tokens.[1].Text}'."))
                | _ -> None)
            |> Option.defaultWith (fun () ->
                raise (DashSpecParseException("Layout module requires scope toolbar|tab|page|card|host after @layout <id>.")))

        let gridBlocks =
            env.Body
            |> Array.choose (function
                | DashSpecAstNode.BlockDeclaration b when b.Opener = DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Grid -> Some b
                | _ -> None)

        if gridBlocks.Length > 1 then
            raise (DashSpecParseException("Layout module allows a single grid block."))

        let gridColumns =
            match gridBlocks with
            | [||] -> 0
            | [| gridBlock |] ->
                let rowLengths = ResizeArray<int>()
                for child in gridBlock.Members do
                    match child with
                    | DashSpecAstNode.GridRowDeclaration r when r.TypedCells |> Array.forall (fun c -> c.Target = "[]" && c.Weight.IsNone) ->
                        rowLengths.Add r.TypedCells.Length
                    | DashSpecAstNode.BlankLine _
                    | DashSpecAstNode.EndBlock _ -> ()
                    | c when DashSpecAst.isTrivia c -> ()
                    | _ ->
                        raise (DashSpecParseException("Layout grid requires the drawing: draw it with [] cells — columns = N is dead."))

                if rowLengths.Count = 0 then
                    raise (DashSpecParseException("Layout grid requires the drawing: draw it with [] cells — columns = N is dead."))

                rowLengths |> Seq.max
            | _ -> 0

        let boardMembers =
            env.Body
            |> Array.filter (function
                | DashSpecAstNode.BlockDeclaration b -> b.Opener <> DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Grid
                | _ -> true)

        let entries = projectBoardEntriesFromAst boardMembers

        if entries.IsEmpty && gridColumns = 0 then
            raise (DashSpecParseException("Layout board requires at least one row [ … ], group { … }, or nest { … }."))

        { Entries = entries :> IReadOnlyList<_>
          ModuleScope = Some scope
          GridColumns = gridColumns }
