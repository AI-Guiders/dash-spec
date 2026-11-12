namespace DashSpec.Modeling.Parse.Lexing

open System
open System.Collections.Concurrent
open DashSpec.Modeling.Core

/// Shared keyword tables for parser-side classification (formatter, syntax, symbols).
module DashSpecKeywords =

    let identEquals (left: string) (right: string) =
        String.Equals(left, right, StringComparison.OrdinalIgnoreCase)

    /// Built-in diagram kind ids of the language; plugins extend the active set via <see cref="registerDiagramKind"/>.
    let diagramKinds =
        [ "line"; "area"; "sparkline"; "bar"; "pie"; "donut"; "doughnut"; "scatter"; "histogram"; "box"
          "boxplot"; "treemap"; "gauge"; "windrose"; "wind_rose"; "table"; "number"; "heatmap"; "gantt" ]

    let private activeDiagramKinds =
        let d = ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        for k in diagramKinds do
            d.[k] <- true
        d

    let registerDiagramKind (kind: string) =
        if not (String.IsNullOrWhiteSpace kind) then
            activeDiagramKinds.[kind.Trim().ToLowerInvariant()] <- true

    let isDiagramKind (kind: string) =
        not (String.IsNullOrWhiteSpace kind) && activeDiagramKinds.ContainsKey(kind.Trim())

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
        | _ -> isDiagramKind value

    /// `module <kind> <id>` roots: resolved from the declarative `stdlib/units/units.ds` manifest.
    let isModuleDirective (value: string) =
        ImportKindRegistry.isUnitKeyword value
