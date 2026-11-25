namespace DashSpec.Modeling.Parse.Template

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Layout
open DashSpec.Modeling.Parse.Syntax

/// <summary>
/// First-class target domain of a <c>module template &lt;id&gt;</c> unit (DASHSPEC-ADR-0139).
/// </summary>
[<RequireQualifiedAccess>]
type TemplateKind =
    | Report
    | Doc

module TemplateKind =

    let tryParse (raw: string) : TemplateKind option =
        match raw.Trim().ToLowerInvariant() with
        | "report" | "dashboard" | "section" -> Some TemplateKind.Report
        | "doc" | "documentation" | "help" -> Some TemplateKind.Doc
        | _ -> None

    let toKeyword (kind: TemplateKind) : string =
        match kind with
        | TemplateKind.Report -> "report"
        | TemplateKind.Doc -> "doc"

/// <summary>
/// Typed chrome &amp; workbench geometry settings inside a <c>module template &lt;id&gt;</c> unit.
/// </summary>
[<CLIMutable>]
type TemplateChromeOptions =
    { ScrollMode: string
      ArticleStyle: string
      TreeWidthPx: int
      TocWidthPx: int
      SidebarWidthPx: int
      ShowBreadcrumbs: bool
      ShowSearch: bool
      ShowToc: bool
      ShowFormatGuide: bool
      ExpandAllChapters: bool }

module TemplateChromeOptions =

    let defaults (kind: TemplateKind) : TemplateChromeOptions =
        match kind with
        | TemplateKind.Doc ->
            { ScrollMode = "independent"
              ArticleStyle = "prose"
              TreeWidthPx = 330
              TocWidthPx = 270
              SidebarWidthPx = 256
              ShowBreadcrumbs = true
              ShowSearch = true
              ShowToc = true
              ShowFormatGuide = false
              ExpandAllChapters = true }
        | TemplateKind.Report ->
            { ScrollMode = "page"
              ArticleStyle = "cards"
              TreeWidthPx = 330
              TocWidthPx = 270
              SidebarWidthPx = 256
              ShowBreadcrumbs = false
              ShowSearch = false
              ShowToc = false
              ShowFormatGuide = true
              ExpandAllChapters = true }

/// <summary>
/// Compiled <c>module template &lt;id&gt;</c> workbench shell specification for <c>report</c> or <c>doc</c>.
/// Defines the responsive slot plane (<c>tree</c>, <c>article</c>, <c>toc</c> for <c>doc</c>;
/// <c>sidebar</c>, <c>tabs</c>, <c>toolbar</c>, <c>board</c> for <c>report</c>) and chrome options.
/// </summary>
[<CLIMutable>]
type TemplateDocument =
    { Id: string
      Namespace: string option
      Kind: TemplateKind
      Board: LayoutBoardDefinition
      Chrome: TemplateChromeOptions }

module TemplateModuleParser =

    let private parseBool (raw: string) (fallback: bool) =
        match raw.Trim().ToLowerInvariant() with
        | "true" | "yes" | "1" -> true
        | "false" | "no" | "0" -> false
        | _ -> fallback

    let private parseInt (raw: string) (fallback: int) =
        let cleaned =
            if raw.EndsWith("px", StringComparison.OrdinalIgnoreCase) then
                raw.Substring(0, raw.Length - 2).Trim()
            else
                raw.Trim()
        match Int32.TryParse cleaned with
        | true, v when v >= 120 && v <= 900 -> v
        | _ -> fallback

    let private defaultBoardForKind (kind: TemplateKind) : LayoutBoardDefinition =
        match kind with
        | TemplateKind.Doc ->
            let wideRow = [| "tree:2"; "article:7"; "toc:2" |] :> IReadOnlyList<string>
            let midRow = [| "tree:2"; "article:6" |] :> IReadOnlyList<string>
            let narrowRow = [| "article:1" |] :> IReadOnlyList<string>
            { Entries = [ CardRow wideRow ] :> IReadOnlyList<_>
              ModuleScope = None
              GridColumns = 0
              MidEntries = Some([ CardRow midRow ] :> IReadOnlyList<_>)
              NarrowEntries = Some([ CardRow narrowRow ] :> IReadOnlyList<_>) }
        | TemplateKind.Report ->
            let wideRow = [| "sidebar:1"; "main:5" |] :> IReadOnlyList<string>
            let mainNest =
                NestRow
                    { Id = "main"
                      Rows =
                          [ [| "tabs" |] :> IReadOnlyList<string>
                            [| "toolbar" |] :> IReadOnlyList<string>
                            [| "board" |] :> IReadOnlyList<string> ]
                          :> IReadOnlyList<_> }
            { Entries = [ mainNest; CardRow wideRow ] :> IReadOnlyList<_>
              ModuleScope = None
              GridColumns = 0
              MidEntries = None
              NarrowEntries = None }

    let private applyChromeProps (baseChrome: TemplateChromeOptions) (members: DashSpecAstNode[]) : TemplateChromeOptions =
        let mutable c = baseChrome
        for m in members do
            match m with
            | DashSpecAstNode.PropertyAssignment p ->
                let v = p.Value.Text.Trim()
                match p.Key.Trim().ToLowerInvariant() with
                | "scroll" | "scroll_mode" -> c <- { c with ScrollMode = v.ToLowerInvariant() }
                | "article_style" | "sections" | "flow" ->
                    let style =
                        match v.ToLowerInvariant() with
                        | "cards" | "card" | "boxed" -> "cards"
                        | _ -> "prose"
                    c <- { c with ArticleStyle = style }
                | "tree_width" -> c <- { c with TreeWidthPx = parseInt v c.TreeWidthPx }
                | "toc_width" -> c <- { c with TocWidthPx = parseInt v c.TocWidthPx }
                | "sidebar_width" -> c <- { c with SidebarWidthPx = parseInt v c.SidebarWidthPx }
                | "breadcrumbs" | "show_breadcrumbs" -> c <- { c with ShowBreadcrumbs = parseBool v c.ShowBreadcrumbs }
                | "search" | "show_search" -> c <- { c with ShowSearch = parseBool v c.ShowSearch }
                | "toc" | "show_toc" | "on_this_page" -> c <- { c with ShowToc = parseBool v c.ShowToc }
                | "format_guide" | "show_format_guide" -> c <- { c with ShowFormatGuide = parseBool v c.ShowFormatGuide }
                | "expand_all" | "expand_chapters" -> c <- { c with ExpandAllChapters = parseBool v c.ExpandAllChapters }
                | _ -> ()
            | _ -> ()
        c

    let parseEnvelope (env: UnitGrammar.UnitEnvelope) : TemplateDocument =
        let mutable kindOpt: TemplateKind option = None

        let headerToks = env.ModuleNode.Tokens
        if headerToks.Length >= 3 && String.Equals(headerToks.[0].Text, "template", StringComparison.OrdinalIgnoreCase) then
            kindOpt <- TemplateKind.tryParse headerToks.[1].Text

        for m in env.Body do
            match m with
            | DashSpecAstNode.PropertyAssignment p when
                String.Equals(p.Key, "kind", StringComparison.OrdinalIgnoreCase)
                || String.Equals(p.Key, "target", StringComparison.OrdinalIgnoreCase)
                || String.Equals(p.Key, "scope", StringComparison.OrdinalIgnoreCase) ->
                match TemplateKind.tryParse p.Value.Text with
                | Some k -> kindOpt <- Some k
                | None ->
                    raise (DashSpecParseException($"Template '{env.Id}': kind must be 'report' or 'doc', got '{p.Value.Text}'."))
            | DashSpecAstNode.ClauseDeclaration { Kind = ClauseKind.DirectiveClause("kind", args) } when args.Length > 0 ->
                match TemplateKind.tryParse args.[0] with
                | Some k -> kindOpt <- Some k
                | None ->
                    raise (DashSpecParseException($"Template '{env.Id}': kind must be 'report' or 'doc', got '{args.[0]}'."))
            | _ -> ()

        let kind =
            kindOpt
            |> Option.defaultWith (fun () ->
                if env.Id.Contains("doc", StringComparison.OrdinalIgnoreCase) then
                    TemplateKind.Doc
                else
                    TemplateKind.Report)

        let mutable chrome = applyChromeProps (TemplateChromeOptions.defaults kind) env.Body
        for b in env.View.Blocks do
            match b.Opener with
            | DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Chrome
            | DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Presentation ->
                chrome <- applyChromeProps chrome b.Members
            | _ -> ()

        let boardMembers =
            env.Body
            |> Array.filter (function
                | DashSpecAstNode.PropertyAssignment p ->
                    let k = p.Key.Trim().ToLowerInvariant()
                    k = "row" || k = "hstack" || k = "vstack" || k = "col"
                | DashSpecAstNode.ClauseDeclaration _ -> false
                | DashSpecAstNode.BlockDeclaration b ->
                    b.Opener <> DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Chrome
                    && b.Opener <> DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Presentation
                | _ -> true)

        let board =
            if boardMembers.Length = 0 then
                defaultBoardForKind kind
            else
                try
                    LayoutModuleParser.projectBoardFromAst boardMembers
                with _ ->
                    defaultBoardForKind kind

        { Id = env.Id
          Namespace = env.Namespace
          Kind = kind
          Board = board
          Chrome = chrome }

    let parse (text: string) : TemplateDocument =
        if String.IsNullOrWhiteSpace text then
            invalidArg "text" "Template module text is required."
        let tree = DashSpecSurfaceSyntax.parse text
        match UnitGrammar.tryUnwrapAny tree.Root with
        | Some env -> parseEnvelope env
        | None ->
            raise (DashSpecParseException("Expected 'module template <id>'."))

    /// Inspect the top-level wide row of a doc template and return ordered slot ids (`tree`, `article`, `toc`)
    /// plus the grid-template-columns CSS string.
    let resolveDocShellColumns (template: TemplateDocument) : string[] * string =
        let rawCells =
            template.Board.Rows
            |> List.tryHead
            |> Option.map Seq.toArray
            |> Option.defaultValue [| "tree:2"; "article:7"; "toc:2" |]

        let slots = ResizeArray<string>()
        let colWidths = ResizeArray<string>()

        for cell in rawCells do
            let name =
                let idx = cell.IndexOf(':')
                if idx > 0 then cell.Substring(0, idx).Trim() else cell.Trim()
            let norm = name.TrimStart('@').ToLowerInvariant()
            match norm with
            | "tree" | "sidebar" | "nav" ->
                slots.Add "tree"
                colWidths.Add $"{template.Chrome.TreeWidthPx}px"
            | "article" | "main" | "content" | "body" ->
                slots.Add "article"
                colWidths.Add "minmax(0, 1fr)"
            | "toc" | "outline" | "on_this_page" ->
                if template.Chrome.ShowToc then
                    slots.Add "toc"
                    colWidths.Add $"{template.Chrome.TocWidthPx}px"
            | _ -> ()

        if slots.Count = 0 then
            [| "tree"; "article"; "toc" |], $"{template.Chrome.TreeWidthPx}px minmax(0, 1fr) {template.Chrome.TocWidthPx}px"
        else
            slots.ToArray(), String.Join(" ", colWidths)
