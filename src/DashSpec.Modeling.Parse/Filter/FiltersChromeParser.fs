namespace DashSpec.Modeling.Parse.Filter

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

module FiltersChromeStickyParser =

    let parse (raw: string option) =
        match raw with
        | None -> FiltersChromeDefinition.StickyNone
        | Some value when String.IsNullOrWhiteSpace value -> FiltersChromeDefinition.StickyNone
        | Some value ->
            match value.Trim().ToLowerInvariant() with
            | "true" | "yes" | "1" -> FiltersChromeDefinition.StickyLine
            | "false" | "no" | "0" -> FiltersChromeDefinition.StickyNone
            | FiltersChromeDefinition.StickyNone -> FiltersChromeDefinition.StickyNone
            | FiltersChromeDefinition.StickyLine -> FiltersChromeDefinition.StickyLine
            | FiltersChromeDefinition.StickyCard -> FiltersChromeDefinition.StickyCard
            | _ ->
                raise (DashSpecParseException("filters chrome sticky must be 'none', 'line', or 'card' (true/false also accepted)."))

module FiltersChromeParser =

    let parse (reader: TokenReader) =
        let props =
            PropertyBlockParser.parseWithEndKind
                reader
                PropertySchemas.filtersChrome
                "filters chrome"
                "chrome"
                false
                false

        let layout =
            match props.TryGetValue "layout" with
            | true, layoutRaw ->
                match layoutRaw.ToLowerInvariant() with
                | "card" | "bar" -> layoutRaw.ToLowerInvariant()
                | _ -> raise (DashSpecParseException("filters chrome layout must be 'card' or 'bar'."))
            | false, _ -> "card"

        let sticky =
            match props.TryGetValue "sticky" with
            | true, stickyRaw -> FiltersChromeStickyParser.parse (Some stickyRaw)
            | false, _ -> FiltersChromeDefinition.StickyNone

        let apply =
            match props.TryGetValue "apply" with
            | true, applyRaw ->
                match applyRaw.ToLowerInvariant() with
                | "manual" | "auto" -> applyRaw.ToLowerInvariant()
                | _ -> raise (DashSpecParseException("filters chrome apply must be 'manual' or 'auto'."))
            | false, _ -> "manual"

        let debounceMs =
            match props.TryGetValue "debounce_ms" with
            | true, debounceRaw ->
                match Int32.TryParse debounceRaw with
                | true, parsed when parsed >= 0 -> parsed
                | _ -> 400
            | false, _ -> 400

        { Layout = layout; Sticky = sticky; Apply = apply; DebounceMs = debounceMs }
