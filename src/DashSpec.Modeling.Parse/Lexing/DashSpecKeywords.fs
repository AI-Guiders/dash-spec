namespace DashSpec.Modeling.Parse.Lexing

open System

/// Shared keyword tables for parser-side classification (formatter, syntax, symbols).
module DashSpecKeywords =

    let identEquals (left: string) (right: string) =
        String.Equals(left, right, StringComparison.OrdinalIgnoreCase)

    let isBlockKeyword (value: string) =
        match value.ToLowerInvariant() with
        | "runtime" | "configuration" | "wiring" | "report" | "extensions" | "links" | "surfaces"
        | "bind" | "filters" | "cards" | "views" | "data" | "transform" | "series"
        | "presentation" | "view" | "layout" | "chrome" | "click" | "inspect"
        | "overrides" | "variables" | "commands" | "standalone" | "toolbar"
        | "diagramlibrary" | "group" | "phase" | "diagram"
        | "heatmap" | "gantt" | "bar" | "line" | "area" | "pie" | "donut" | "gauge" | "kpi"
        | "table" | "scatter" | "treemap" | "windrose" | "box" | "histogram" | "number" -> true
        | _ -> false

    let isControlKeyword (value: string) =
        match value.ToLowerInvariant() with
        | "tab" | "as" | "card" | "cards" | "dashspec" | "filter" | "show" | "use"
        | "include" | "import" | "on" | "goto" | "page" | "phase" | "group"
        | "connector" | "palette" | "manifest" | "sqldialect" | "datasource" | "link" | "catalog"
        | "diagram" | "end" -> true
        | _ -> isBlockKeyword value

    let isModuleDirective (value: string) =
        match value.ToLowerInvariant() with
        | "dashboard" | "tab" | "catalog" | "diagram" | "presentation" | "host" | "layout" -> true
        | _ -> false
