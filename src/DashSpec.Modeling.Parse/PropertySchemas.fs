namespace DashSpec.Modeling.Parse

open System
open System.Collections.Generic

/// Property schemas for Modeling.Parse (ADR-0048 SSOT).
module PropertySchemas =

    type PropertyValueType =
        | Scalar
        | String
        | DateRange
        | QualifiedName
        | CommaList
        | RestOfLine
        | ColumnBinding

    type PropertySpec = { Name: string; ValueType: PropertyValueType }

    let private spec name valueType = { Name = name; ValueType = valueType }

    let seriesTransform =
        [ spec "use" Scalar
          spec "max" Scalar
          spec "other" String ]

    /// Core presentation vocabulary — engine-owned semantics only. Renderer plugins
    /// declare their own knobs (IDashSpecPlugin.PresentationProperties) and the kernel
    /// extends this list through the bridge: one knob, one owner.
    let private presentationCore =
        [ spec "use" Scalar
          spec "legend" Scalar
          spec "height" Scalar
          spec "max_height" Scalar
          spec "visible_rows" Scalar
          spec "stretch_mode" Scalar
          spec "stretch_direction" Scalar
          spec "stacked" Scalar
          spec "fill" Scalar
          spec "color_mode" Scalar
          spec "scale_value" Scalar
          spec "y_max" Scalar
          spec "default" Scalar
          spec "colors" Scalar
          spec "width" Scalar
          spec "density" Scalar
          spec "column_filters" Scalar
          spec "grouping" Scalar
          spec "align" Scalar ]

    let private extraPresentationProperties = HashSet<string>(StringComparer.OrdinalIgnoreCase)

    /// Plugin-declared presentation knobs join the vocabulary (bridge pattern — the
    /// kernel registers them when it builds parse options from active plugins).
    let registerPresentationProperties (properties: seq<string>) =
        for property in properties do
            if not (String.IsNullOrWhiteSpace property) then
                extraPresentationProperties.Add(property.Trim()) |> ignore

    /// The effective presentation schema: core + plugin-declared, all Scalar.
    let presentation () =
        let extras =
            extraPresentationProperties
            |> Seq.filter (fun name -> presentationCore |> List.exists (fun s -> String.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)) |> not)
            |> Seq.map (fun name -> spec name Scalar)
            |> List.ofSeq
        presentationCore @ extras

    let layoutGroup = [ spec "title" String ]

    let placement =
        [ spec "row" Scalar
          spec "col" Scalar
          spec "span" Scalar ]

    let cardLimits =
        [ spec "cells" Scalar
          spec "axis" Scalar ]

    let filterDate =
        [ spec "column" ColumnBinding
          spec "widget" Scalar // date-domain function (day | range), ADR-0118
          spec "grain_filter" Scalar ]

    let filterField =
        [ spec "column" ColumnBinding
          spec "widget" Scalar // legacy hint, migrates to density (ADR-0118)
          spec "density" Scalar // selector expression: inline | collapsed (ADR-0118)
          spec "single" Scalar ]

    let filterTop =
        [ spec "min" Scalar
          spec "max" Scalar ]

    let filterBindDate =
        [ spec "column" ColumnBinding
          spec "grain_filter" Scalar
          spec "single" Scalar ]

    let filterBindField =
        [ spec "column" ColumnBinding
          spec "single" Scalar ]

    let filterShow =
        [ spec "apply" Scalar
          spec "label" String
          spec "widget" Scalar // legacy hint (ADR-0118)
          spec "density" Scalar // selector expression: inline | collapsed (ADR-0118)
          spec "ref" Scalar
          spec "bind_scope" Scalar ]

    let filtersChromeApply =
        [ spec "mode" Scalar
          spec "control" Scalar ]

    let filtersChrome =
        [ spec "layout" Scalar
          spec "sticky" Scalar
          spec "debounce_ms" Scalar
          spec "format_guide" Scalar
          spec "cells" Scalar ]

    let cardChrome =
        [ spec "bound_filters" Scalar
          spec "title" Scalar
          spec "fold" Scalar
          spec "slot_expand" Scalar ]

    let cardsChrome = [ spec "fold" Scalar ]

    let legend =
        [ spec "min" String
          spec "max" String
          spec "title" String
          spec "show" Scalar ]

    let runtime =
        [ spec "manifest" String ]

    let configuration =
        [ spec "sqldialect" Scalar
          spec "diagramlibrary" String
          spec "render" Scalar
          spec "time_basis" Scalar
          spec "time_apply" Scalar
          spec "work_time_column" ColumnBinding
          spec "work_timezone" Scalar
          spec "work_start" Scalar
          spec "work_end" Scalar
          spec "work_days" Scalar ]

    let hostConfiguration =
        [ spec "language" Scalar
          spec "display_timezone" Scalar ]

    let hostPresentation =
        [ spec "product_title" String
          spec "catalog_label" String
          spec "color_scheme" Scalar
          spec "large_field_filter_layout" Scalar
          spec "render" Scalar ]

    /// <summary>Host `menu` block (ADR-0073 nav surface): sidebar composition policy.
    /// Rides to the shell chrome as `menu_*` presentation keys.</summary>
    let hostMenu =
        [ spec "sections" Scalar
          spec "collapse_duplicate" Scalar ]

    /// <summary>Settings home block (`host … end host`, ADR-0112 §6): single-token keys of the block
    /// grammar (`render`, `color_scheme` — established names), folded over the legacy blocks at bootstrap.</summary>
    let hostSettings =
        [ spec "language" Scalar
          spec "display_timezone" Scalar
          spec "product_title" String
          spec "catalog_label" String
          spec "large_field_filter_layout" Scalar
          spec "color_scheme" Scalar
          spec "render" Scalar
          spec "provider_default" Scalar ]

    let hostLink =
        [ spec "url" String
          spec "target" String
          spec "topbar" Scalar
          spec "settings" Scalar ]

    let palette =
        [ spec "colors" String
          spec "default" String ]

    let chartDiagram =
        [ spec "x" ColumnBinding
          spec "y" ColumnBinding
          spec "category" ColumnBinding
          spec "value" ColumnBinding
          spec "series" ColumnBinding
          spec "reference" ColumnBinding
          spec "color" ColumnBinding
          spec "size" ColumnBinding
          spec "legend" Scalar
          spec "max_series" Scalar
          spec "stacked" Scalar
          spec "fill" Scalar
          spec "bins" Scalar
          spec "bin_width" Scalar
          spec "height" Scalar
          spec "aggregate" Scalar
          spec "min" Scalar
          spec "max" Scalar ]

    let tableDiagram =
        [ spec "columns" CommaList
          spec "order_by" RestOfLine
          spec "limit" Scalar
          spec "title" String
          spec "value" ColumnBinding
          spec "aggregate" Scalar ]

    let numberDiagram =
        [ spec "value" ColumnBinding
          spec "aggregate" Scalar
          spec "scale_value" Scalar
          spec "delta" Scalar ]

    let heatmapDiagram =
        [ spec "x" ColumnBinding
          spec "y" ColumnBinding
          spec "value" ColumnBinding
          spec "height" Scalar
          spec "visible_rows" Scalar
          spec "color_normalize" Scalar
          spec "axis_scroll" Scalar ]

    let ganttDiagram =
        [ spec "y" ColumnBinding
          spec "from" ColumnBinding
          spec "to" ColumnBinding
          spec "color" ColumnBinding
          spec "height" Scalar
          spec "order_by" RestOfLine
          spec "limit" Scalar ]

    /// Mirrors Core PropertyBlockParser.ResolveEndKind for block containers.
    let resolveEndKind (blockName: string) =
        let parts = blockName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
        if parts.Length = 0 then blockName
        else
            match parts.[0] with
            | "transform" -> "transform"
            | "filters" when parts.Length > 1 && parts.[1] = "chrome" -> "chrome"
            | "filter" -> "filter"
            | "bind" -> "bind"
            | "diagram" -> parts.[parts.Length - 1]
            | "layout" when parts.Length > 1 && parts.[1] = "grid" -> "grid"
            | "layout" -> "layout"
            | "place" -> "place"
            | "series" -> "series"
            | "formats" -> "formats"
            | "toolbar" -> parts.[parts.Length - 1]
            | _ when parts.Length > 1 -> parts.[parts.Length - 1]
            | _ -> blockName
