namespace DashSpec.Modeling.Parse.Syntax

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Diagram
open DashSpec.Modeling.Parse.Lexing

/// <summary>
/// Declarative syntax grammar graph (DASHSPEC-ADR-0134):
/// defines container nesting rules and token-by-token line transitions over the typed <see cref="ParseTree"/>.
/// </summary>
module DashSpecGrammarGraph =

    [<RequireQualifiedAccess>]
    type GrammarScope =
        | Root
        | Module of kind: DashSpecModuleKind
        | Block of keyword: DashSpecBlockKeyword * blockId: string option * parent: GrammarScope

    [<RequireQualifiedAccess>]
    type GrammarSymbolKind =
        | UnitOfKind of unitKeyword: string
        | Namespace
        | RowType
        | DeclaredFilter
        | DeclaredCard
        | DeclaredFlowNode
        | DeclaredPort of role: PortDeclRole

    [<RequireQualifiedAccess>]
    type GrammarExpectation =
        | Keyword of word: string * detail: string
        | PropertyKey of spec: PropertySchemas.PropertySpec * context: string
        | ClosingKeyword of word: string
        | UnitKindKeyword
        | ImportKindKeyword
        | TransformPluginId
        | DiagramKindId
        | SymbolRef of kind: GrammarSymbolKind
        | ValueDomain of propertyKey: string * values: string list

    type GrammarQueryResult =
        { Scope: GrammarScope
          EnclosingBlock: BlockDeclarationSyntax option
          Prefix: string
          Expectations: GrammarExpectation list }

    let private kw word detail = GrammarExpectation.Keyword(word, detail)

    let private schemaProps (specs: PropertySchemas.PropertySpec list) (ctx: string) =
        specs |> List.map (fun s -> GrammarExpectation.PropertyKey(s, ctx))

    let private isFlowModuleScope (scope: GrammarScope) =
        let rec loop s =
            match s with
            | GrammarScope.Module(DashSpecModuleKind.Flow _) -> true
            | GrammarScope.Block(_, _, parent) -> loop parent
            | _ -> false
        loop scope

    /// Declarative table of valid statement starter tokens for each <see cref="DashSpecModuleKind"/>.
    let private moduleStarters (kind: DashSpecModuleKind) : GrammarExpectation list =
        let modWord = DashSpecModuleKind.toKeyword kind
        let headerStarters =
            [ kw "namespace" "Module namespace"
              kw "import" "import <kind> from <Namespace>"
              kw "from" "from <Namespace> import <kind>"
              GrammarExpectation.ClosingKeyword modWord ]

        let bodyStarters =
            match kind with
            | DashSpecModuleKind.Dashboard
            | DashSpecModuleKind.Section ->
                [ kw "runtime" "Runtime manifest block"
                  kw "configuration" "Module configuration block"
                  kw "aliases" "Aliases block"
                  kw "extensions" "Module extensions block"
                  kw "connect" "Connect palette, flow, and layout"
                  kw "report" "Report body block" ]
            | DashSpecModuleKind.Flow _ ->
                [ kw "defaults" "Flow defaults block"
                  kw "source" "Flow data source"
                  kw "transformer" "Flow transformer"
                  kw "flow" "Nested subprocess flow"
                  GrammarExpectation.SymbolRef GrammarSymbolKind.DeclaredFlowNode ]
            | DashSpecModuleKind.Diagram ->
                [ kw "chrome" "Chart chrome preset"
                  kw "presentation" "Diagram presentation block"
                  GrammarExpectation.DiagramKindId ]
            | DashSpecModuleKind.Presentation ->
                (kw "use" "use <presentation_preset>")
                :: schemaProps (PropertySchemas.presentation ()) "presentation"
            | DashSpecModuleKind.Types ->
                [ kw "type" "Row type definition" ]
            | DashSpecModuleKind.Layout ->
                [ kw "scope" "scope host | toolbar | section | page | card"
                  kw "grid" "Grid layout block"
                  kw "group" "Group container"
                  kw "nest" "Nest container"
                  kw "hstack" "Horizontal slot stack"
                  kw "vstack" "Vertical slot stack"
                  kw "wide" "Wide breakpoint board"
                  kw "mid" "Mid breakpoint board"
                  kw "narrow" "Narrow breakpoint board" ]
            | DashSpecModuleKind.Host ->
                [ kw "catalog" "Catalog path"
                  kw "host" "Host settings block"
                  kw "configuration" "Host configuration block"
                  kw "presentation" "Host presentation block"
                  kw "menu" "Host menu policy block"
                  kw "links" "External links block"
                  kw "surfaces" "Visible host surfaces block" ]
            | DashSpecModuleKind.Catalog ->
                [ kw "default" "Default catalog entry id"
                  kw "group" "Catalog group block"
                  kw "entry" "Catalog entry declaration" ]
            | DashSpecModuleKind.Runtime ->
                [ kw "default" "Default provider id"
                  kw "bundle" "Active plugin bundle"
                  kw "plugins" "Enabled plugins list"
                  kw "provider" "Provider configuration block" ]
            | DashSpecModuleKind.Defaults ->
                (kw "use" "use <defaults_preset>")
                :: schemaProps PropertySchemas.configuration "defaults"
            | DashSpecModuleKind.Palette ->
                schemaProps PropertySchemas.palette "palette"
            | DashSpecModuleKind.Transform ->
                [ kw "locality" "Transform locality (database | runtime)"
                  kw "type_effect" "Transform type_effect"
                  kw "requires_block" "Whether parameter block is required"
                  kw "hint" "Parameter validation hint"
                  kw "param" "Transform parameter declaration" ]
            | DashSpecModuleKind.Tooltip ->
                [ kw "variables" "Tooltip variables block"
                  kw "template" "Tooltip template string" ]
            | DashSpecModuleKind.Texts ->
                [ kw "language" "Locale language tag" ]
            | DashSpecModuleKind.Project ->
                [ kw "host" "Project entry host module id (host = <id>)"
                  kw "source_roots" "Project source roots list"
                  kw "files" "Project included files globs"
                  kw "exclude" "Project excluded files globs"
                  kw "global" "Global implicit import modifier" ]
            | DashSpecModuleKind.Registry ->
                []
            | DashSpecModuleKind.Template ->
                [ kw "kind" "Template target kind (report | doc)"
                  kw "chrome" "Template chrome options block"
                  kw "wide" "Wide viewport layout board ([ tree:2 article:7 toc:2 ] or [ sidebar:1 main:5 ])"
                  kw "mid" "Mid viewport layout board"
                  kw "narrow" "Narrow viewport layout board"
                  kw "nest" "Nested slot container (e.g. nest main … end nest)"
                  kw "hstack" "Horizontal slot stack"
                  kw "vstack" "Vertical slot stack"
                  kw "scroll" "Scroll mode (independent | page)"
                  kw "article_style" "Article layout flow (prose | cards)"
                  kw "tree_width" "Left navigation tree width in px (doc)"
                  kw "toc_width" "Right On-This-Page outline width in px (doc)"
                  kw "sidebar_width" "Left report sidebar width in px (report)"
                  kw "breadcrumbs" "Show breadcrumb trail (true | false)"
                  kw "search" "Show symbol search box (true | false)"
                  kw "toc" "Show right On-This-Page outline (true | false)" ]
            | DashSpecModuleKind.Documentation ->
                [ kw "title" "Documentation book title"
                  kw "use" "use template <template_id>"
                  kw "template" "template = <template_id>"
                  kw "page" "Documentation chapter/page block"
                  kw "section" "Documentation section block" ]
            | DashSpecModuleKind.Hotkeys ->
                [ kw "notation" "Default gesture notation (auto | key-gesture | vim | neovim-kbd | emacs-kbd)"
                  kw "chord_root" "Keyboard chord root"
                  kw "hotkey" "hotkey <id> … end hotkey" ]
            | DashSpecModuleKind.Custom "commands" ->
                [ kw "planet" "Command planet id"
                  kw "flavor" "Command grammar flavor"
                  kw "chord_root" "Keyboard chord root"
                  kw "bindings" "Keyboard bindings table"
                  kw "phrases" "Command phrases table"
                  kw "commands" "Command aliases table" ]
            | DashSpecModuleKind.Custom _ ->
                []

        headerStarters @ bodyStarters

    /// Declarative table of valid statement starter tokens for each <see cref="DashSpecBlockKeyword"/>.
    let private blockStarters (keyword: DashSpecBlockKeyword) (blockId: string option) (parent: GrammarScope) : GrammarExpectation list =
        let endName = DashSpecBlockKeyword.toEndName keyword
        let closeStarter = [ GrammarExpectation.ClosingKeyword endName ]

        let bodyStarters =
            match keyword with
            | DashSpecBlockKeyword.Connect ->
                [ kw "use" "use palette | flow"
                  kw "layout" "layout grid | board" ]
            | DashSpecBlockKeyword.Runtime ->
                schemaProps PropertySchemas.runtime "runtime"
            | DashSpecBlockKeyword.Configuration ->
                match parent with
                | GrammarScope.Module DashSpecModuleKind.Host ->
                    schemaProps PropertySchemas.hostConfiguration "host.configuration"
                | _ ->
                    schemaProps PropertySchemas.configuration "configuration"
            | DashSpecBlockKeyword.Report ->
                [ kw "title" "Report title"
                  kw "type" "Inline row type"
                  kw "standalone" "Standalone tab filters block"
                  kw "page" "Report page block"
                  kw "phase" "Report phase block"
                  kw "section" "Report section reference/block"
                  kw "commands" "Command aliases block"
                  kw "defaults" "Report defaults block"
                  kw "cards" "cards chrome block"
                  kw "filters" "Filters table or chrome"
                  kw "toolbar" "Toolbar placement block"
                  kw "layout" "Report layout grid"
                  kw "palette" "Report color palette"
                  kw "filter" "Filter declaration block"
                  kw "card" "Card declaration block"
                  kw "data" "data flow block"
                  kw "placement" "placement flow block"
                  kw "show" "show flow block"
                  kw "wire" "wire flow block"
                  kw "action" "action flow block" ]
            | DashSpecBlockKeyword.Page ->
                match parent with
                | GrammarScope.Module DashSpecModuleKind.Documentation ->
                    [ kw "title" "Chapter title"
                      kw "symbols" "symbols = units | transforms | presentations | templates | types | hotkeys | monitoring | rules | workspace | all"
                      kw "role" "Filter units by role: declaration | consumer | root"
                      kw "locality" "Filter transforms by locality: database | runtime"
                      kw "severity" "Filter rules by severity: error | warning | info"
                      kw "kind" "Filter symbols by module/symbol kind"
                      kw "namespace" "Filter symbols by namespace prefix"
                      kw "group_by" "Group symbols by: role | locality | severity | kind | namespace | none"
                      kw "report" "Embed live .ds report entry"
                      kw "section" "Nested documentation section block" ]
                | _ ->
                    [ kw "title" "Page title"
                      kw "layout" "Page layout board"
                      kw "toolbar" "toolbar chrome"
                      kw "derive" "Usage date derive rule"
                      kw "bind" "bind display block"
                      kw "defaults" "Page filter defaults"
                      kw "filter" "Page filter declaration"
                      kw "phase" "Page phase block"
                      kw "card" "Page card declaration"
                      kw "data" "data flow block"
                      kw "placement" "placement flow block"
                      kw "show" "show flow block"
                      kw "wire" "wire flow block"
                      kw "action" "action flow block" ]
            | DashSpecBlockKeyword.Phase ->
                [ kw "card" "Phase card declaration"
                  kw "filter" "Phase filter declaration"
                  kw "layout" "Phase layout" ]
            | DashSpecBlockKeyword.Standalone ->
                [ kw "defaults" "Standalone filter defaults"
                  kw "filter" "Standalone filter declaration"
                  kw "toolbar" "Standalone toolbar"
                  kw "filters" "Standalone filters" ]
            | DashSpecBlockKeyword.Card ->
                [ kw "title" "Card title"
                  kw "chrome" "Card chrome block"
                  kw "when" "Conditional visibility"
                  kw "limits" "Card matrix limits"
                  kw "on" "on click interaction"
                  kw "use" "use <card_preset>"
                  kw "view" "Card view block"
                  kw "override" "Diagram override block"
                  kw "overrides" "Diagram overrides block"
                  kw "bind" "Card filter bindings"
                  kw "filters" "Card local filters chrome"
                  kw "diagram" "Bind diagram module"
                  kw "place" "Card grid placement"
                  kw "layout" "Card interior layout"
                  kw "legend" "Card legend block"
                  kw "presentation" "Card presentation block"
                  kw "inspect" "Card inspect block"
                  kw "tooltip" "Inline tooltip block"
                  kw "data" "data flow block"
                  kw "placement" "placement flow block"
                  kw "show" "show flow block"
                  kw "wire" "wire flow block"
                  kw "action" "action flow block" ]
            | DashSpecBlockKeyword.View
            | DashSpecBlockKeyword.Views ->
                [ kw "diagram" "Diagram reference"
                  kw "legend" "Legend block" ]
            | DashSpecBlockKeyword.Filter ->
                [ kw "bind" "bind field | date | top"
                  kw "show" "Filter UI display block"
                  kw "place" "Filter grid placement" ]
            | DashSpecBlockKeyword.Bind ->
                match parent with
                | GrammarScope.Block(DashSpecBlockKeyword.Filter, _, _) ->
                    schemaProps PropertySchemas.filterBindField "filter.bind"
                    @ schemaProps PropertySchemas.filterBindDate "filter.bind"
                    @ schemaProps PropertySchemas.filterTop "filter.bind"
                | _ ->
                    [ GrammarExpectation.SymbolRef GrammarSymbolKind.DeclaredFilter ]
            | DashSpecBlockKeyword.Show ->
                schemaProps PropertySchemas.filterShow "filter.show"
            | DashSpecBlockKeyword.Presentation ->
                match parent with
                | GrammarScope.Module DashSpecModuleKind.Host ->
                    schemaProps PropertySchemas.hostPresentation "host.presentation"
                | _ ->
                    schemaProps (PropertySchemas.presentation ()) "presentation"
            | DashSpecBlockKeyword.Chrome ->
                match parent with
                | GrammarScope.Block(DashSpecBlockKeyword.Filters, _, _)
                | GrammarScope.Block(DashSpecBlockKeyword.Toolbar, _, _) ->
                    schemaProps PropertySchemas.filtersChrome "filters.chrome"
                | GrammarScope.Block(DashSpecBlockKeyword.Card, _, _) ->
                    schemaProps PropertySchemas.cardChrome "card.chrome"
                | GrammarScope.Block(DashSpecBlockKeyword.Cards, _, _) ->
                    schemaProps PropertySchemas.cardsChrome "cards.chrome"
                | GrammarScope.Module DashSpecModuleKind.Template ->
                    [ kw "scroll" "Scroll mode (independent | page)"
                      kw "article_style" "Article layout flow (prose | cards)"
                      kw "tree_width" "Left navigation tree width in px (doc)"
                      kw "toc_width" "Right On-This-Page outline width in px (doc)"
                      kw "sidebar_width" "Left report sidebar width in px (report)"
                      kw "breadcrumbs" "Show breadcrumb trail (true | false)"
                      kw "search" "Show symbol search box (true | false)"
                      kw "toc" "Show right On-This-Page outline (true | false)"
                      kw "expand_all" "Expand all chapters initially (true | false)" ]
                | _ ->
                    (kw "use" "use <presentation_preset>")
                    :: schemaProps (PropertySchemas.presentation ()) "chrome"
            | DashSpecBlockKeyword.Defaults ->
                schemaProps PropertySchemas.configuration "defaults"
                @ [ kw "provider" "Default provider"
                    kw "time_zone" "Default time zone (utc | local)" ]
            | DashSpecBlockKeyword.Host ->
                schemaProps PropertySchemas.hostSettings "host"
            | DashSpecBlockKeyword.Menu ->
                schemaProps PropertySchemas.hostMenu "menu"
            | DashSpecBlockKeyword.Links ->
                [ kw "link" "Host link block" ]
            | DashSpecBlockKeyword.Link ->
                schemaProps PropertySchemas.hostLink "link"
            | DashSpecBlockKeyword.Surfaces ->
                [ kw "show" "show <surface1>, <surface2>" ]
            | DashSpecBlockKeyword.Source ->
                [ kw "use" "use provider <id|infer>"
                  kw "from" "from view | sql | xlsx"
                  kw "ports" "Source output ports block"
                  kw "time_zone" "Source time zone (utc | local)"
                  kw "provider" "Source provider id"
                  kw "on" "Join condition columns"
                  kw "left_key" "Left join key columns"
                  kw "right_key" "Right join key columns" ]
            | DashSpecBlockKeyword.Transformer ->
                [ kw "ports" "Transformer ports block"
                  kw "transform" "transform use <plugin>" ]
            | DashSpecBlockKeyword.Transform ->
                match blockId with
                | Some pluginId ->
                    match TransformDeclarations.tryFind pluginId with
                    | Some decl ->
                        decl.Params
                        |> List.map (fun p ->
                            GrammarExpectation.PropertyKey(
                                { Name = p.Key; ValueType = PropertySchemas.PropertyValueType.Scalar },
                                p.Hint))
                    | None -> []
                | None -> []
            | DashSpecBlockKeyword.Series ->
                schemaProps PropertySchemas.seriesTransform "series"
            | DashSpecBlockKeyword.Ports ->
                [ kw "input" "input stream|scalar <name>: <Type>"
                  kw "output" "output stream <name>: <Type>"
                  kw "param" "param <name>: <type>"
                  kw "default" "default input|output <port>" ]
            | DashSpecBlockKeyword.Type ->
                [ kw "optional" "Optional field modifier"
                  kw "string" "string field"
                  kw "int" "int field"
                  kw "decimal" "decimal field"
                  kw "float" "float field"
                  kw "bool" "bool field"
                  kw "date" "date field"
                  kw "datetime" "datetime field"
                  kw "array" "fixed array field" ]
            | DashSpecBlockKeyword.Flow ->
                if isFlowModuleScope parent then
                    [ kw "source" "Nested flow source"
                      kw "transformer" "Nested flow transformer"
                      GrammarExpectation.SymbolRef GrammarSymbolKind.DeclaredFlowNode ]
                else
                    [ GrammarExpectation.SymbolRef GrammarSymbolKind.DeclaredFilter
                      GrammarExpectation.SymbolRef GrammarSymbolKind.DeclaredFlowNode ]
            | DashSpecBlockKeyword.Layout
            | DashSpecBlockKeyword.Grid
            | DashSpecBlockKeyword.Group
            | DashSpecBlockKeyword.Nest
            | DashSpecBlockKeyword.VStack
            | DashSpecBlockKeyword.HStack
            | DashSpecBlockKeyword.Wide
            | DashSpecBlockKeyword.Mid
            | DashSpecBlockKeyword.Narrow ->
                [ kw "columns" "Grid column count"
                  kw "gap" "Grid gap"
                  kw "group" "Group container"
                  kw "nest" "Nest container"
                  kw "hstack" "Horizontal stack"
                  kw "vstack" "Vertical stack"
                  kw "wide" "Wide breakpoint"
                  kw "mid" "Mid breakpoint"
                  kw "narrow" "Narrow breakpoint"
                  kw "place" "Explicit placement"
                  GrammarExpectation.SymbolRef GrammarSymbolKind.DeclaredCard ]
            | DashSpecBlockKeyword.OnClick
            | DashSpecBlockKeyword.Click ->
                [ kw "set" "set <filter> from <field>"
                  kw "add" "add <filter> from <field> (<C-LMB> / sequential filter stacking)"
                  kw "focus" "focus <phase>"
                  kw "goto" "goto <page>"
                  kw "drill" "drill <slot>" ]
            | DashSpecBlockKeyword.Table ->
                schemaProps PropertySchemas.tableDiagram "diagram table"
            | DashSpecBlockKeyword.Columns ->
                [ kw "id" "Column field id"
                  kw "label" "Column header label"
                  kw "format" "Column value format"
                  kw "size" "Column width mode (content | header | cells | fill)"
                  kw "align" "Column alignment (left | center | right)"
                  kw "sorting" "Column sort direction (ASC | DESC)" ]
            | DashSpecBlockKeyword.Other name ->
                match name.ToLowerInvariant() with
                | "place" -> schemaProps PropertySchemas.placement "place"
                | "limits" -> schemaProps PropertySchemas.cardLimits "limits"
                | "legend" -> schemaProps PropertySchemas.legend "legend"
                | "section" ->
                    [ kw "title" "Section title"
                      kw "symbols" "symbols = units | transforms | presentations | types | monitoring | rules | workspace | all"
                      kw "role" "Filter units by role: declaration | consumer | root"
                      kw "locality" "Filter transforms by locality: database | runtime"
                      kw "severity" "Filter rules by severity: error | warning | info"
                      kw "kind" "Filter symbols by module/symbol kind"
                      kw "namespace" "Filter symbols by namespace prefix"
                      kw "group_by" "Group symbols by: role | locality | severity | kind | namespace | none"
                      kw "report" "Embed live .ds report entry"
                      kw "section" "Nested documentation section block" ]
                | _ ->
                    match DiagramKindRegistry.tryResolve name with
                    | true, spec -> schemaProps spec.Properties $"diagram {spec.Id}"
                    | false, _ -> []
            | DashSpecBlockKeyword.Tab ->
                match parent with
                | GrammarScope.Module DashSpecModuleKind.Documentation
                | GrammarScope.Block(_, _, GrammarScope.Module DashSpecModuleKind.Documentation) ->
                    [ kw "title" "Section title"
                      kw "symbols" "symbols = units | transforms | presentations | templates | types | hotkeys | monitoring | rules | workspace | all"
                      kw "role" "Filter units by role: declaration | consumer | root"
                      kw "locality" "Filter transforms by locality: database | runtime"
                      kw "severity" "Filter rules by severity: error | warning | info"
                      kw "kind" "Filter symbols by module/symbol kind"
                      kw "namespace" "Filter symbols by namespace prefix"
                      kw "group_by" "Group symbols by: role | locality | severity | kind | namespace | none"
                      kw "report" "Embed live .ds report entry"
                      kw "section" "Nested documentation section block" ]
                | _ -> []
            | _ -> []

        closeStarter @ bodyStarters

    /// Declarative container starters for any <see cref="GrammarScope"/>.
    let containerStarters (scope: GrammarScope) : GrammarExpectation list =
        match scope with
        | GrammarScope.Root ->
            [ kw "module" "Declare unit module" ]
        | GrammarScope.Module kind ->
            moduleStarters kind
        | GrammarScope.Block(keyword, blockId, parent) ->
            blockStarters keyword blockId parent

    /// Declarative value domain for known scalar properties.
    let propertyValueDomain (propertyKey: string) : GrammarExpectation list =
        match propertyKey.Trim().ToLowerInvariant() with
        | "flows" | "flow" ->
            [ GrammarExpectation.SymbolRef(GrammarSymbolKind.UnitOfKind "flow") ]
        | "palette" ->
            [ GrammarExpectation.SymbolRef(GrammarSymbolKind.UnitOfKind "palette") ]
        | "bind" ->
            [ GrammarExpectation.SymbolRef GrammarSymbolKind.DeclaredFilter ]
        | "widget" ->
            [ GrammarExpectation.ValueDomain("widget", [ "combobox"; "chips"; "select"; "day"; "range" ]) ]
        | "density" ->
            [ GrammarExpectation.ValueDomain("density", [ "compact"; "comfortable"; "inline"; "collapsed" ]) ]
        | "size" | "width" ->
            [ GrammarExpectation.ValueDomain(propertyKey, [ "content"; "fill" ]) ]
        | "column_size" ->
            [ GrammarExpectation.ValueDomain("column_size", [ "content"; "header"; "cells"; "fill" ]) ]
        | "align" ->
            [ GrammarExpectation.ValueDomain("align", [ "left"; "center"; "right" ]) ]
        | "grid_lines" ->
            [ GrammarExpectation.ValueDomain("grid_lines", [ "both"; "x"; "y"; "none" ]) ]
        | "markers" ->
            [ GrammarExpectation.ValueDomain("markers", [ "true"; "false" ]) ]
        | "legend" ->
            [ GrammarExpectation.ValueDomain("legend", [ "bottom"; "top"; "left"; "right"; "none" ]) ]
        | "orientation" ->
            [ GrammarExpectation.ValueDomain("orientation", [ "horizontal"; "vertical" ]) ]
        | "color_scheme" ->
            [ GrammarExpectation.ValueDomain("color_scheme", [ "dark"; "light"; "contrast" ]) ]
        | "render" ->
            [ GrammarExpectation.ValueDomain("render", [ "radzen"; "echarts"; "selfmade" ]) ]
        | "sqldialect" | "sql_dialect" ->
            [ GrammarExpectation.ValueDomain("sqldialect", [ "tsql"; "postgres"; "sqlite" ]) ]
        | "time_zone" | "timezone" | "time_basis" ->
            [ GrammarExpectation.ValueDomain(propertyKey, [ "utc"; "local" ]) ]
        | "provider" | "provider_default" | "default_provider" ->
            [ GrammarExpectation.ValueDomain(propertyKey, [ "infer"; "sqlserver"; "postgres" ]) ]
        | "type_effect" ->
            [ GrammarExpectation.ValueDomain("type_effect", [ "identity"; "projection"; "extend"; "compose"; "require_equal"; "opaque" ]) ]
        | "locality" ->
            [ GrammarExpectation.ValueDomain("locality", [ "database"; "runtime" ]) ]
        | "symbols" | "category" ->
            [ GrammarExpectation.ValueDomain("symbols", [ "units"; "transforms"; "presentations"; "templates"; "types"; "hotkeys"; "monitoring"; "rules"; "workspace"; "all" ]) ]
        | "notation" ->
            [ GrammarExpectation.ValueDomain("notation", [ "auto"; "key-gesture"; "vim"; "neovim-kbd"; "emacs-kbd" ]) ]
        | "kind" ->
            [ GrammarExpectation.ValueDomain("kind", [ "report"; "doc" ]) ]
        | "scroll" | "scroll_mode" ->
            [ GrammarExpectation.ValueDomain("scroll", [ "independent"; "page" ]) ]
        | "article_style" | "sections" ->
            [ GrammarExpectation.ValueDomain("article_style", [ "prose"; "cards" ]) ]
        | "role" ->
            [ GrammarExpectation.ValueDomain("role", [ "declaration"; "consumer"; "root" ]) ]
        | "severity" ->
            [ GrammarExpectation.ValueDomain("severity", [ "error"; "warning"; "info" ]) ]
        | "group_by" | "groupby" ->
            [ GrammarExpectation.ValueDomain("group_by", [ "role"; "locality"; "severity"; "kind"; "namespace"; "none" ]) ]
        | "single" | "stacked" | "fill" | "sticky" | "fold" | "collapse_duplicate" | "topbar" | "settings" | "requires_block" ->
            [ GrammarExpectation.ValueDomain(propertyKey, [ "true"; "false" ]) ]
        | "apply" | "mode" ->
            [ GrammarExpectation.ValueDomain(propertyKey, [ "auto"; "manual" ]) ]
        | _ -> []

    /// Declarative token-sequence transitions within a line.
    let lineTransitions (scope: GrammarScope) (lineTokens: SyntaxToken list) : GrammarExpectation list =
        match lineTokens with
        | [] -> containerStarters scope
        | tokens ->
            let lastTok = List.last tokens
            let secondLastTok = if tokens.Length >= 2 then Some tokens.[tokens.Length - 2] else None
            let eqIdx = tokens |> List.tryFindIndex (fun t -> t.LexKind = TokenKind.Eq)

            // 1. After `=` assignment
            match eqIdx with
            | Some idx when idx > 0 ->
                match tokens.[0].Keyword with
                | Some(DashSpecKeyword.Block DashSpecBlockKeyword.Source) ->
                    [ kw "View" "View(\"schema.view\")"
                      kw "SqlFile" "SqlFile(\"path.sql\")"
                      kw "SqlQuery" "SqlQuery(\"SELECT ...\")"
                      kw "Xlsx" "Xlsx(\"file.xlsx\")"
                      kw "InnerJoin" "InnerJoin(left, right)"
                      kw "LeftJoin" "LeftJoin(left, right)"
                      kw "RightJoin" "RightJoin(left, right)"
                      kw "FullJoin" "FullJoin(left, right)"
                      kw "UnionAll" "UnionAll(left, right)" ]
                | _ ->
                    let keyName = tokens |> List.take idx |> List.map (fun t -> t.Text) |> String.concat ""
                    propertyValueDomain keyName
            | _ ->
                // 2. After `:` type annotation
                if lastTok.LexKind = TokenKind.Colon
                   || (secondLastTok.IsSome && secondLastTok.Value.LexKind = TokenKind.Colon) then
                    [ GrammarExpectation.SymbolRef GrammarSymbolKind.RowType ]
                // 3. After `->` flow arrow
                elif tokens |> List.exists (fun t -> t.LexKind = TokenKind.FlowArrow) then
                    if isFlowModuleScope scope then
                        [ GrammarExpectation.SymbolRef GrammarSymbolKind.DeclaredFlowNode ]
                    else
                        [ kw "__diagram__" "Card implicit diagram slot"
                          GrammarExpectation.SymbolRef GrammarSymbolKind.DeclaredCard ]
                else
                    // 4. Prefix keyword automaton
                    let kwPattern =
                        tokens
                        |> List.map (fun t ->
                            match t.Keyword with
                            | Some k -> Choice1Of2 k
                            | None -> Choice2Of2(t.Text.ToLowerInvariant()))

                    match scope, kwPattern with
                    // `module <unit_kind>`
                    | _, [ Choice1Of2 DashSpecKeyword.Module ] ->
                        [ GrammarExpectation.UnitKindKeyword ]

                    // `end <closing_keyword>`
                    | GrammarScope.Block(blkKw, _, _), [ Choice1Of2 DashSpecKeyword.End ] ->
                        [ GrammarExpectation.ClosingKeyword(DashSpecBlockKeyword.toEndName blkKw) ]
                    | GrammarScope.Module modKind, [ Choice1Of2 DashSpecKeyword.End ] ->
                        [ GrammarExpectation.ClosingKeyword(DashSpecModuleKind.toKeyword modKind) ]

                    // `import <import_kind> from <Namespace>`
                    | _, [ Choice1Of2 DashSpecKeyword.Import ] ->
                        [ GrammarExpectation.ImportKindKeyword ]
                    | _, [ Choice1Of2 DashSpecKeyword.Import; _ ] ->
                        [ kw "from" "from <Namespace>" ]
                    | _, Choice1Of2 DashSpecKeyword.Import :: _ :: Choice1Of2 DashSpecKeyword.From :: _ ->
                        [ GrammarExpectation.SymbolRef GrammarSymbolKind.Namespace ]

                    // `from ...` inside `source` vs module header
                    | GrammarScope.Block(DashSpecBlockKeyword.Source, _, _), [ Choice1Of2 DashSpecKeyword.From ] ->
                        [ kw "view" "from view <schema.view>"
                          kw "sql" "from sql file | query"
                          kw "xlsx" "from xlsx file" ]
                    | GrammarScope.Block(DashSpecBlockKeyword.Source, _, _), [ Choice1Of2 DashSpecKeyword.From; Choice2Of2 "sql" ] ->
                        [ kw "file" "from sql file \"path.sql\""
                          kw "query" "from sql query \"SELECT ...\"" ]
                    | _, [ Choice1Of2 DashSpecKeyword.From ] ->
                        [ GrammarExpectation.SymbolRef GrammarSymbolKind.Namespace ]
                    | _, [ Choice1Of2 DashSpecKeyword.From; _ ] ->
                        [ kw "import" "import <kind>" ]
                    | _, [ Choice1Of2 DashSpecKeyword.From; _; Choice1Of2 DashSpecKeyword.Import ] ->
                        [ GrammarExpectation.ImportKindKeyword ]

                    // `ports` block transitions: `default`, `input`, `output`
                    | GrammarScope.Block(DashSpecBlockKeyword.Ports, _, _), [ Choice1Of2 DashSpecKeyword.Default ] ->
                        [ kw "input" "Default input port"
                          kw "output" "Default output port" ]
                    | GrammarScope.Block(DashSpecBlockKeyword.Ports, _, _), [ Choice1Of2 DashSpecKeyword.Default; Choice1Of2 DashSpecKeyword.Input ] ->
                        [ GrammarExpectation.SymbolRef(GrammarSymbolKind.DeclaredPort PortDeclRole.PortInput) ]
                    | GrammarScope.Block(DashSpecBlockKeyword.Ports, _, _), [ Choice1Of2 DashSpecKeyword.Default; Choice1Of2 DashSpecKeyword.Output ] ->
                        [ GrammarExpectation.SymbolRef(GrammarSymbolKind.DeclaredPort PortDeclRole.PortOutput) ]
                    | GrammarScope.Block(DashSpecBlockKeyword.Ports, _, _), [ Choice1Of2 DashSpecKeyword.Input ] ->
                        [ kw "stream" "Stream input port"
                          kw "scalar" "Scalar input port" ]
                    | GrammarScope.Block(DashSpecBlockKeyword.Ports, _, _), [ Choice1Of2 DashSpecKeyword.Output ] ->
                        [ kw "stream" "Stream output port" ]

                    // `transform` transitions: `transform use <plugin>` inside `transformer`
                    | GrammarScope.Block(DashSpecBlockKeyword.Transformer, _, _), [ Choice1Of2(DashSpecKeyword.Block DashSpecBlockKeyword.Transform) ] ->
                        [ kw "use" "transform use <plugin>" ]
                    | _, [ Choice1Of2(DashSpecKeyword.Block DashSpecBlockKeyword.Transform); Choice1Of2 DashSpecKeyword.Use ] ->
                        [ GrammarExpectation.TransformPluginId ]

                    // `use` transitions by scope
                    | GrammarScope.Block(DashSpecBlockKeyword.Source, _, _), [ Choice1Of2 DashSpecKeyword.Use ]
                    | GrammarScope.Block(DashSpecBlockKeyword.Defaults, _, _), [ Choice1Of2 DashSpecKeyword.Use ]
                    | GrammarScope.Module(DashSpecModuleKind.Flow _), [ Choice1Of2 DashSpecKeyword.Use ] ->
                        [ kw "provider" "use provider <id|infer>" ]
                    | GrammarScope.Block(DashSpecBlockKeyword.Connect, _, _), [ Choice1Of2 DashSpecKeyword.Use ] ->
                        [ kw "palette" "use palette <PaletteId>"
                          kw "flow" "use flow <FlowId>" ]
                    | GrammarScope.Block(DashSpecBlockKeyword.Chrome, _, _), [ Choice1Of2 DashSpecKeyword.Use ]
                    | GrammarScope.Block(DashSpecBlockKeyword.Presentation, _, _), [ Choice1Of2 DashSpecKeyword.Use ]
                    | GrammarScope.Module DashSpecModuleKind.Presentation, [ Choice1Of2 DashSpecKeyword.Use ] ->
                        [ GrammarExpectation.SymbolRef(GrammarSymbolKind.UnitOfKind "presentation") ]
                    | _, [ Choice1Of2 DashSpecKeyword.Use ] ->
                        [ kw "flow" "use flow <FlowId>"
                          kw "palette" "use palette <PaletteId>"
                          kw "provider" "use provider <id|infer>"
                          GrammarExpectation.SymbolRef(GrammarSymbolKind.UnitOfKind "presentation") ]
                    | _, [ Choice1Of2 DashSpecKeyword.Use; Choice1Of2(DashSpecKeyword.Block DashSpecBlockKeyword.Flow) ] ->
                        [ GrammarExpectation.SymbolRef(GrammarSymbolKind.UnitOfKind "flow") ]
                    | _, [ Choice1Of2 DashSpecKeyword.Use; Choice1Of2(DashSpecKeyword.Block DashSpecBlockKeyword.Palette) ] ->
                        [ GrammarExpectation.SymbolRef(GrammarSymbolKind.UnitOfKind "palette") ]
                    | _, [ Choice1Of2 DashSpecKeyword.Use; Choice1Of2(DashSpecKeyword.Block DashSpecBlockKeyword.Provider) ] ->
                        [ GrammarExpectation.ValueDomain("provider", [ "infer"; "sqlserver"; "postgres" ]) ]

                    // `diagram` transitions
                    | _, [ Choice1Of2(DashSpecKeyword.Block DashSpecBlockKeyword.Diagram) ] ->
                        [ kw "ref" "diagram ref <slot> <diagram_id>"
                          GrammarExpectation.SymbolRef(GrammarSymbolKind.UnitOfKind "diagram") ]
                    | _, [ Choice1Of2(DashSpecKeyword.Block DashSpecBlockKeyword.Diagram); Choice2Of2 "ref"; _ ] ->
                        [ GrammarExpectation.SymbolRef(GrammarSymbolKind.UnitOfKind "diagram") ]

                    // `bind` transitions by scope
                    | GrammarScope.Block(DashSpecBlockKeyword.Filter, _, _), [ Choice1Of2(DashSpecKeyword.Block DashSpecBlockKeyword.Bind) ] ->
                        [ kw "field" "Field filter binding"
                          kw "date" "Date range filter binding"
                          kw "top" "Top-N limit filter binding" ]
                    | GrammarScope.Block(DashSpecBlockKeyword.Page, _, _), [ Choice1Of2(DashSpecKeyword.Block DashSpecBlockKeyword.Bind) ] ->
                        [ kw "display" "bind display block" ]
                    | _, [ Choice1Of2(DashSpecKeyword.Block DashSpecBlockKeyword.Bind) ] ->
                        [ GrammarExpectation.SymbolRef GrammarSymbolKind.DeclaredFilter ]

                    // `layout` inside `connect` vs general
                    | GrammarScope.Block(DashSpecBlockKeyword.Connect, _, _), [ Choice1Of2(DashSpecKeyword.Block DashSpecBlockKeyword.Layout) ] ->
                        [ kw "grid" "layout grid"
                          kw "board" "layout board" ]
                    | _, [ Choice1Of2(DashSpecKeyword.Block DashSpecBlockKeyword.Layout) ] ->
                        [ GrammarExpectation.SymbolRef(GrammarSymbolKind.UnitOfKind "layout") ]

                    // `palette <id>`
                    | _, [ Choice1Of2(DashSpecKeyword.Block DashSpecBlockKeyword.Palette) ] ->
                        [ GrammarExpectation.SymbolRef(GrammarSymbolKind.UnitOfKind "palette") ]

                    // `scope <kind>`
                    | _, [ Choice1Of2 DashSpecKeyword.Scope ] ->
                        [ kw "host" "Host shell layout scope"
                          kw "toolbar" "Toolbar layout scope"
                          kw "section" "Section layout scope"
                          kw "page" "Page layout scope"
                          kw "card" "Card interior layout scope" ]

                    // `on click`
                    | _, [ Choice1Of2 DashSpecKeyword.On ] ->
                        [ kw "click" "on click block" ]

                    // `cards chrome` / `toolbar chrome` / `filters chrome|dashboard`
                    | _, [ Choice1Of2(DashSpecKeyword.Block DashSpecBlockKeyword.Cards) ]
                    | _, [ Choice1Of2(DashSpecKeyword.Block DashSpecBlockKeyword.Toolbar) ] ->
                        [ kw "chrome" "chrome block" ]
                    | _, [ Choice1Of2(DashSpecKeyword.Block DashSpecBlockKeyword.Filters) ] ->
                        [ kw "chrome" "filters chrome block"
                          kw "dashboard" "filters dashboard block" ]

                    // Qualified flow header: `data|placement|show|wire|action flow`
                    | _, [ Choice1Of2(DashSpecKeyword.Block DashSpecBlockKeyword.Data) ]
                    | _, [ Choice1Of2(DashSpecKeyword.Block DashSpecBlockKeyword.Show) ]
                    | _, [ Choice2Of2 "placement" ]
                    | _, [ Choice2Of2 "wire" ]
                    | _, [ Choice2Of2 "action" ] ->
                        [ kw "flow" "Qualified flow block" ]

                    | _ -> []

    let describeExpectation (exp: GrammarExpectation) : string =
        match exp with
        | GrammarExpectation.Keyword(word, _) -> $"'{word}'"
        | GrammarExpectation.ClosingKeyword word -> $"'{word}'"
        | GrammarExpectation.PropertyKey(spec, _) -> $"'{spec.Name}'"
        | GrammarExpectation.ValueDomain(_, values) ->
            values |> List.map (fun v -> $"'{v}'") |> String.concat " | "
        | GrammarExpectation.UnitKindKeyword -> "<unit_kind>"
        | GrammarExpectation.ImportKindKeyword -> "<import_kind>"
        | GrammarExpectation.DiagramKindId -> "<diagram_kind>"
        | GrammarExpectation.TransformPluginId -> "<transform_plugin>"
        | GrammarExpectation.SymbolRef sym ->
            match sym with
            | GrammarSymbolKind.Namespace -> "<Namespace>"
            | GrammarSymbolKind.UnitOfKind kind -> $"<{kind}_id>"
            | GrammarSymbolKind.RowType -> "<RowType>"
            | GrammarSymbolKind.DeclaredFilter -> "<filter_id>"
            | GrammarSymbolKind.DeclaredCard -> "<card_id>"
            | GrammarSymbolKind.DeclaredFlowNode -> "<node_id>"
            | GrammarSymbolKind.DeclaredPort role ->
                match role with
                | PortDeclRole.PortInput -> "<input_port>"
                | PortDeclRole.PortOutput -> "<output_port>"
                | PortDeclRole.PortParam -> "<param_port>"

    /// Diagnose an unrecognized statement node against the declarative grammar transition graph:
    /// walks the node's tokens left-to-right until a token violates the expected transitions of <paramref name="scope"/>,
    /// returning the offending token's span and a precise `expected ...` message derived from <see cref="lineTransitions"/>.
    let diagnoseStatementTokens (scope: GrammarScope) (node: DashSpecAstNode) : TextSpan * string =
        let tokens =
            DashSpecAst.tokens node
            |> Array.filter (fun t ->
                t.LexKind <> TokenKind.LineComment
                && t.LexKind <> TokenKind.DocComment
                && t.LexKind <> TokenKind.BlockComment
                && t.LexKind <> TokenKind.Newline
                && t.LexKind <> TokenKind.Eof)

        if tokens.Length = 0 then
            DashSpecAst.span node, "Unexpected empty statement."
        else
            let matchesExpectation (tok: SyntaxToken) (exp: GrammarExpectation) =
                match exp with
                | GrammarExpectation.Keyword(word, _)
                | GrammarExpectation.ClosingKeyword word ->
                    String.Equals(tok.Text, word, StringComparison.OrdinalIgnoreCase)
                | GrammarExpectation.PropertyKey(spec, _) ->
                    String.Equals(tok.Text, spec.Name, StringComparison.OrdinalIgnoreCase)
                | GrammarExpectation.ValueDomain(_, values) ->
                    values |> List.exists (fun v -> String.Equals(tok.Text, v, StringComparison.OrdinalIgnoreCase))
                | GrammarExpectation.UnitKindKeyword ->
                    tok.LexKind = TokenKind.Ident
                | GrammarExpectation.ImportKindKeyword ->
                    ImportKindRegistry.allDefinitions
                    |> Array.exists (fun d -> String.Equals(tok.Text, d.Keyword, StringComparison.OrdinalIgnoreCase))
                | GrammarExpectation.DiagramKindId ->
                    fst (DiagramKindRegistry.tryResolve tok.Text)
                | GrammarExpectation.TransformPluginId ->
                    TransformDeclarations.isKnown tok.Text
                | GrammarExpectation.SymbolRef _ ->
                    tok.LexKind = TokenKind.Ident

            let rec walkPrefix (prefix: SyntaxToken list) (idx: int) =
                let expected = lineTransitions scope prefix
                if idx >= tokens.Length then
                    let lastTok = tokens.[tokens.Length - 1]
                    if expected.IsEmpty then
                        let lineText = tokens |> Array.map (fun t -> t.Text) |> String.concat " "
                        DashSpecAst.span node, $"Incomplete or unexpected statement '{lineText}'."
                    else
                        let expStr = expected |> List.map describeExpectation |> List.distinct |> String.concat " or "
                        lastTok.Span, $"Incomplete statement after '{lastTok.Text}' (expected {expStr})."
                else
                    let current = tokens.[idx]
                    if expected |> List.exists (matchesExpectation current) then
                        walkPrefix (prefix @ [ current ]) (idx + 1)
                    elif not expected.IsEmpty then
                        let expStr = expected |> List.map describeExpectation |> List.distinct |> String.concat " or "
                        let afterCtx =
                            if prefix.IsEmpty then ""
                            else
                                let prevText = prefix |> List.map (fun t -> t.Text) |> String.concat " "
                                $" after '{prevText}'"
                        current.Span, $"Unexpected '{current.Text}'{afterCtx} (expected {expStr})."
                    else
                        let lineText = tokens |> Array.map (fun t -> t.Text) |> String.concat " "
                        current.Span, $"Unexpected token '{current.Text}' in '{lineText}'."

            walkPrefix [] 0

    let private resolveScopeAndEnclosingBlock (tree: ParseTree) (caretOffset: int) : GrammarScope * BlockDeclarationSyntax option =
        let probe = max 0 (caretOffset - 1)
        let mutable scope = GrammarScope.Root
        let mutable enclosingBlock: BlockDeclarationSyntax option = None

        let rec walk (node: DashSpecAstNode) =
            let span = DashSpecAst.span node
            if (span.Start <= caretOffset && caretOffset <= span.End)
               || (span.Start <= probe && probe <= span.End) then
                match node with
                | DashSpecAstNode.ModuleDeclaration m when caretOffset > m.HeaderSpan.End ->
                    let kind =
                        match m.Directive with
                        | DashSpecModuleDirective.Dashboard _ -> DashSpecModuleKind.Dashboard
                        | DashSpecModuleDirective.Tab _ -> DashSpecModuleKind.Section
                        | DashSpecModuleDirective.Module(k, _) -> k
                    scope <- GrammarScope.Module kind
                | DashSpecAstNode.BlockDeclaration b when caretOffset > b.HeaderSpan.End ->
                    let kw, blkId =
                        match b.Opener with
                        | DashSpecBlockOpener.Named(k, id) -> k, Some id
                        | DashSpecBlockOpener.Anonymous k -> k, None
                    scope <- GrammarScope.Block(kw, blkId, scope)
                    enclosingBlock <- Some b
                | _ -> ()

                for child in DashSpecAst.members node do
                    walk child

        walk tree.Root
        scope, enclosingBlock

    let private extractLineTokensAndPrefix (tree: ParseTree) (caretOffset: int) : SyntaxToken list * string =
        let text = tree.Text
        let clamped = max 0 (min caretOffset text.Length)
        let mutable lineStart = clamped
        while lineStart > 0 && text.[lineStart - 1] <> '\n' do
            lineStart <- lineStart - 1

        let lineTokens =
            tree.Tokens
            |> Array.filter (fun t ->
                t.Span.Start >= lineStart
                && t.Span.Start < clamped
                && t.LexKind <> TokenKind.LineComment
                && t.LexKind <> TokenKind.DocComment
                && t.LexKind <> TokenKind.BlockComment)
            |> Array.toList

        match List.rev lineTokens with
        | [] -> [], ""
        | last :: revRest ->
            if (last.LexKind = TokenKind.Ident || last.LexKind = TokenKind.Raw)
               && last.Span.Start < clamped
               && clamped <= last.Span.End then
                let len = max 0 (min (clamped - last.Span.Start) last.Text.Length)
                List.rev revRest, last.Text.Substring(0, len)
            else
                lineTokens, ""

    /// Compute the active <see cref="GrammarScope"/>, current token prefix, and expected next tokens at <paramref name="caretOffset"/>.
    let expectedAt (tree: ParseTree) (caretOffset: int) : GrammarQueryResult =
        let scope, enclosingBlock = resolveScopeAndEnclosingBlock tree caretOffset
        let completedLineTokens, prefix = extractLineTokensAndPrefix tree caretOffset
        let expectations = lineTransitions scope completedLineTokens
        { Scope = scope
          EnclosingBlock = enclosingBlock
          Prefix = prefix
          Expectations = expectations }
