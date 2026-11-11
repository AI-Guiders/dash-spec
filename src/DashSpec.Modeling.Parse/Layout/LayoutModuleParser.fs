namespace DashSpec.Modeling.Parse.Layout

open System
open System.IO
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing
open DashSpec.Modeling.Parse.Syntax
open System.Collections.Generic

/// <summary>M2 pilot — F# lex + parse SSOT for <c>.dashlayout</c> (ADR-0048).</summary>
module LayoutModuleParser =

    let private parseMandatoryScope (reader: TokenReader) =
        if not (reader.TryKeyword "scope") then
            raise (DashSpec.Modeling.Core.DashSpecParseException("Layout module requires scope toolbar|tab|page|card|host after @layout <id>."))

        let kind = reader.ReadIdent()

        if String.IsNullOrWhiteSpace kind then
            raise (DashSpec.Modeling.Core.DashSpecParseException("Layout module requires scope toolbar|tab|page|card|host after @layout <id>."))

        match kind.ToLowerInvariant() with
        | "toolbar" -> LayoutScope.Toolbar
        | "section" -> LayoutScope.Tab
        | "page" -> LayoutScope.Page
        | "card" -> LayoutScope.Card
        | "host" -> LayoutScope.Host
        | _ ->
            raise (DashSpec.Modeling.Core.DashSpecParseException($"Layout module scope must be toolbar, tab, page, card, or host; got '{kind}'."))

    /// <summary>ADR-0130 slice 3: project the layout board from the typed tree —
    /// grid rows are GridRowDeclaration nodes, group/nest blocks carry their title
    /// properties and member rows; None when the tree holds no layout module.</summary>
    let rec private tryProjectBoardEntriesFromAst (members: DashSpecAstNode[]) : LayoutBoardEntry list option =
        let entries = ResizeArray<LayoutBoardEntry>()
        let mutable ok = true

        for memberNode in members do
            if ok then
                match memberNode with
                | DashSpecAstNode.GridRowDeclaration row -> entries.Add(CardRow(row.Cells :> IReadOnlyList<string>))
                | DashSpecAstNode.BlockDeclaration block ->
                    match block.Opener with
                    | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Group, groupId) ->
                        // legacy readTitleToken unwrapped string literals — same here
                        let unwrap (v: string) =
                            if v.Length >= 2 && v.StartsWith("\"") && v.EndsWith("\"") then v.Substring(1, v.Length - 2) else v

                        let title =
                            block.Members
                            |> Seq.choose (function
                                | DashSpecAstNode.PropertyAssignment p when p.Key = "title" -> Some(unwrap p.ValueText)
                                | _ -> None)
                            |> Seq.tryHead

                        let rows =
                            block.Members
                            |> Seq.choose (function
                                | DashSpecAstNode.GridRowDeclaration r -> Some(r.Cells :> IReadOnlyList<string>)
                                | _ -> None)
                            |> Seq.toArray

                        if rows.Length = 0 then ok <- false
                        else
                            entries.Add(
                                GroupRow
                                    { Id = groupId
                                      Title = title
                                      Rows = (rows :> IReadOnlyList<IReadOnlyList<string>>) })
                    | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Other "nest", nestId) ->
                        let rows =
                            block.Members
                            |> Seq.choose (function
                                | DashSpecAstNode.GridRowDeclaration r -> Some(r.Cells :> IReadOnlyList<string>)
                                | _ -> None)
                            |> Seq.toArray

                        if rows.Length = 0 then ok <- false
                        else entries.Add(NestRow { Id = nestId; Rows = (rows :> IReadOnlyList<IReadOnlyList<string>>) })
                    | _ -> ok <- false
                | DashSpecAstNode.BlankLine _
                | DashSpecAstNode.EndBlock _
                | DashSpecAstNode.ClauseDeclaration _ -> ()
                | memberNode when DashSpecAst.isTrivia memberNode -> ()
                | _ -> ok <- false

        if ok then Some(List.ofSeq entries) else None

    let private tryProjectLayoutFromAst (root: DashSpecAstNode) : LayoutBoardDefinition option =
        match
            DashSpecAst.descendants root
            |> Seq.tryPick (function
                | DashSpecAstNode.ModuleDeclaration m as node ->
                    match m.Directive with
                    | DashSpecModuleDirective.Unit("layout", layoutId) -> Some(node, layoutId)
                    | _ -> None
                | _ -> None)
        with
        | None -> None
        | Some (moduleNode, layoutId) ->
            let scope =
                DashSpecAst.descendants moduleNode
                |> Seq.choose (function
                    | DashSpecAstNode.ClauseDeclaration c ->
                        match c.Kind with
                        | ClauseKind.ScopeClause s -> Some s
                        | _ -> None
                    | _ -> None)
                |> Seq.tryHead

            let scopeKind =
                match scope |> Option.map (fun s -> s.Trim().ToLowerInvariant()) with
                | Some "toolbar" -> Some LayoutScope.Toolbar
                | Some "section" -> Some LayoutScope.Tab
                | Some "page" -> Some LayoutScope.Page
                | Some "card" -> Some LayoutScope.Card
                | Some "host" -> Some LayoutScope.Host
                | _ -> None

            match scopeKind with
            | None -> None
            | Some scope ->
                let gridColumns =
                    DashSpecAst.descendants moduleNode
                    |> Seq.choose (function
                        | DashSpecAstNode.BlockDeclaration b when
                            b.Opener = DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Grid ->
                            b.Members
                            |> Seq.choose (function
                                | DashSpecAstNode.GridRowDeclaration r -> Some r.Cells.Length
                                | _ -> None)
                            |> Seq.tryHead
                        | _ -> None)
                    |> Seq.tryHead
                    |> Option.defaultValue 0

                let boardMembers =
                    DashSpecAst.members moduleNode
                    |> Array.filter (function
                        | DashSpecAstNode.BlockDeclaration b ->
                            b.Opener <> DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Grid
                        | _ -> true)

                match tryProjectBoardEntriesFromAst boardMembers with
                | Some entries when entries.Length > 0 ->
                    Some
                        { Entries = entries
                          ModuleScope = Some scope
                          GridColumns = gridColumns }
                | _ -> None

    /// <summary>Parse a <c>.dashlayout</c> file body.</summary>
    let parseLayoutFile (text: string) : LayoutBoardDefinition =
        if String.IsNullOrWhiteSpace text then
            invalidArg "text" "Layout text is required."

        // ADR-0130 slice 3: tree-first — the surface parse feeds the projection;
        // the token path remains for anything the tree does not cover.
        let tree = DashSpecSurfaceSyntax.parse text

        match tryProjectLayoutFromAst tree.Root with
        | Some board -> board
        | None ->
        let reader = ParserUtilities.createReader text
        reader.SkipFileDirectives()
        reader.ExpectKeyword "module"
        reader.ExpectKeyword "layout"

        let id = reader.ReadIdent()

        if String.IsNullOrWhiteSpace id then
            raise (DashSpec.Modeling.Core.DashSpecParseException("Layout module requires @layout <id>."))

        reader.SkipNewlines()
        let scope = parseMandatoryScope reader
        reader.SkipNewlines()

        // The grid drawing block ([ADR-0111] §2): `grid [] [] … end grid` before the board —
        // the module's own grid, drawn and visible; optional here, the host default serves documents.
        let mutable gridColumns = 0
        while reader.TryKeyword "grid" do
            if gridColumns > 0 then
                raise (DashSpec.Modeling.Core.DashSpecParseException("Layout module allows a single grid block."))
            let grid = LayoutParser.parseGridBody reader
            gridColumns <- grid.Columns
            reader.SkipNewlines()

        let board = LayoutParser.parseBoardRows reader None None
        { board with ModuleScope = Some scope; GridColumns = gridColumns }

