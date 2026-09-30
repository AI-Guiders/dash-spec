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

module FiltersChromeApplyParser =

    let parseMode (raw: string) =
        match raw.Trim().ToLowerInvariant() with
        | "manual" | "auto" -> raw.Trim().ToLowerInvariant()
        | _ -> raise (DashSpecParseException("filters chrome apply mode must be 'manual' or 'auto'."))

    let parseControl (raw: string) =
        match raw.Trim().ToLowerInvariant() with
        | "icon" | "button" -> raw.Trim().ToLowerInvariant()
        | _ ->
            raise (DashSpecParseException("filters chrome apply control must be 'icon' or 'button' (manual apply only)."))

    let parseBlock (reader: TokenReader) =
        let props =
            PropertyBlockParser.parseWithEndKind
                reader
                PropertySchemas.filtersChromeApply
                "filters chrome apply"
                "apply"
                false
                false
                None

        let mode =
            match props.TryGetValue "mode" with
            | true, raw -> parseMode raw
            | false, _ -> "manual"

        let control =
            match props.TryGetValue "control" with
            | true, raw -> parseControl raw
            | false, _ -> "icon"

        mode, control

module FiltersChromeParser =

    let private parseLayout (raw: string) =
        match raw.ToLowerInvariant() with
        | "card" | "bar" -> raw.ToLowerInvariant()
        | _ -> raise (DashSpecParseException("filters chrome layout must be 'card' or 'bar'."))

    let private parseDebounce (raw: string) =
        match Int32.TryParse raw with
        | true, parsed when parsed >= 0 -> parsed
        | _ -> 400

    let private parseFormatGuide (raw: string) =
        match raw.Trim().ToLowerInvariant() with
        | FiltersChromeDefinition.FormatGuideShow -> FiltersChromeDefinition.FormatGuideShow
        | FiltersChromeDefinition.FormatGuideHidden -> FiltersChromeDefinition.FormatGuideHidden
        | _ -> raise (DashSpecParseException("filters chrome format_guide must be 'show' or 'hidden'."))

    let private parseCells (raw: string) =
        match raw.Trim().ToLowerInvariant() with
        | "labeled" | "inline" -> raw.Trim().ToLowerInvariant()
        | _ -> raise (DashSpecParseException("filters chrome cells must be 'labeled' or 'inline'."))

    let parse (reader: TokenReader) =
        let props =
            BlockGrammar.parseKeywordContainer
                reader
                "chrome"
                "filters chrome"
                [ BlockGrammar.KeywordScalarOrBlock(
                    "apply",
                    "apply",
                    "filters chrome apply",
                    (fun r values ->
                        r.Expect TokenKind.Eq
                        values.["apply"] <- FiltersChromeApplyParser.parseMode (r.ReadScalarValue())),
                    (fun r values ->
                        let mode, control = FiltersChromeApplyParser.parseBlock r
                        values.["apply"] <- mode
                        values.["apply_control"] <- control)
                  )
                  BlockGrammar.SchemaProperties(PropertySchemas.filtersChrome, false, false) ]
                None

        let layout =
            match props.TryGetValue "layout" with
            | true, raw -> parseLayout raw
            | false, _ -> "card"

        let sticky =
            match props.TryGetValue "sticky" with
            | true, raw -> FiltersChromeStickyParser.parse (Some raw)
            | false, _ -> FiltersChromeDefinition.StickyNone

        let apply =
            match props.TryGetValue "apply" with
            | true, raw -> raw
            | false, _ -> "manual"

        let applyControl =
            match props.TryGetValue "apply_control" with
            | true, raw -> FiltersChromeApplyParser.parseControl raw
            | false, _ -> "icon"

        let debounceMs =
            match props.TryGetValue "debounce_ms" with
            | true, raw -> parseDebounce raw
            | false, _ -> 400

        let formatGuide =
            match props.TryGetValue "format_guide" with
            | true, raw -> parseFormatGuide raw
            | false, _ -> FiltersChromeDefinition.FormatGuideHidden

        let cells =
            match props.TryGetValue "cells" with
            | true, raw -> parseCells raw
            | false, _ -> ""

        { Layout = layout
          Sticky = sticky
          Apply = apply
          ApplyControl = applyControl
          DebounceMs = debounceMs
          FormatGuide = formatGuide
          Cells = cells }
