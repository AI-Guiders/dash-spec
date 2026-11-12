namespace DashSpec.Modeling.Parse.Lexing

open System
open DashSpec.Modeling.Core

/// Shared keyword tables for parser-side classification (formatter, syntax, symbols).
module DashSpecKeywords =

    let identEquals (left: string) (right: string) =
        String.Equals(left, right, StringComparison.OrdinalIgnoreCase)

    /// Diagram kind ids of the language (ADR-0101). `DiagramKindRegistry` owns their properties and
    /// binding rules; this is only the vocabulary the lexer/formatter classifies.
    let diagramKinds =
        [ "line"; "area"; "sparkline"; "bar"; "pie"; "donut"; "doughnut"; "scatter"; "histogram"; "box"
          "boxplot"; "treemap"; "gauge"; "windrose"; "wind_rose"; "table"; "number"; "heatmap"; "gantt" ]

    /// Typed SSOT for block vocabulary is `DashSpecBlockKeyword.tryOfName`; this table is only
    /// syntax-coloring vocabulary (ADR-0130): control words plus every block keyword so the
    /// highlighter marks block openers (grid, filters, cards, …) as keywords.
    let isControlKeyword (value: string) =
        match value.ToLowerInvariant() with
        | "section" | "sections" | "as" | "card" | "cards" | "dashspec" | "filter" | "show" | "use"
        | "import" | "from" | "namespace" | "on" | "goto" | "page" | "phase" | "group"
        | "connector" | "provider" | "palette" | "manifest" | "sqldialect" | "datasource" | "link" | "catalog"
        | "diagram" | "end"
        | "grid" | "runtime" | "configuration" | "connect" | "report" | "extensions" | "links" | "surfaces"
        | "bind" | "filters" | "views" | "data" | "transform" | "series"
        | "presentation" | "view" | "layout" | "chrome" | "click" | "inspect"
        | "overrides" | "variables" | "commands" | "standalone" | "toolbar"
        | "diagramlibrary" | "menu"
        | "type" | "source" | "transformer" | "ports" | "tooltip" | "host" | "defaults"
        | "flow" | "nest" -> true
        | _ -> diagramKinds |> List.contains (value.ToLowerInvariant())

    /// `module <kind> <id>` roots: every importable unit kind plus the module-only roots.
    let isModuleDirective (value: string) =
        ImportKindRegistry.tryFindByModuleKeyword value |> Option.isSome
        || (match value.ToLowerInvariant() with
            | "dashboard" | "catalog" | "host" -> true
            | _ -> false)
