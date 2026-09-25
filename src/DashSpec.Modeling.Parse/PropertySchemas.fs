namespace DashSpec.Modeling.Parse

open System

/// Property schemas aligned with Core.Parsing.PropertySchemas (ADR-0048).
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

    let presentation =
        [ spec "use" Scalar
          spec "legend" Scalar
          spec "height" Scalar
          spec "viewport_y" Scalar
          spec "stacked" Scalar
          spec "fill" Scalar
          spec "color_mode" Scalar
          spec "scale_value" Scalar
          spec "y_max" Scalar
          spec "default" Scalar
          spec "colors" Scalar ]

    let layoutGrid =
        [ spec "columns" Scalar
          spec "gap" Scalar ]

    let placement =
        [ spec "row" Scalar
          spec "col" Scalar
          spec "span" Scalar ]

    let cardLimits =
        [ spec "cells" Scalar
          spec "axis" Scalar ]

    let filterDate =
        [ spec "column" ColumnBinding
          spec "widget" Scalar
          spec "grain_filter" Scalar ]

    let filterField =
        [ spec "column" ColumnBinding
          spec "widget" Scalar
          spec "single" Scalar ]

    let filterTop =
        [ spec "min" Scalar
          spec "max" Scalar ]

    let filterBindDate =
        [ spec "column" ColumnBinding
          spec "grain_filter" Scalar ]

    let filterBindField =
        [ spec "column" ColumnBinding
          spec "single" Scalar ]

    let filterShow =
        [ spec "label" String
          spec "widget" Scalar
          spec "ref" Scalar ]

    let filtersChrome =
        [ spec "layout" Scalar
          spec "sticky" Scalar
          spec "apply" Scalar
          spec "debounce_ms" Scalar ]

    let legend =
        [ spec "min" String
          spec "max" String
          spec "title" String ]

    let runtime =
        [ spec "manifest" String ]

    let configuration =
        [ spec "sqldialect" Scalar
          spec "palette" String
          spec "diagramlibrary" String ]

    let hostConfiguration =
        [ spec "language" Scalar
          spec "display_timezone" Scalar ]

    let hostPresentation =
        [ spec "product_title" String
          spec "catalog_label" String
          spec "color_scheme" Scalar
          spec "large_field_filter_layout" Scalar ]

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
          spec "limit" Scalar ]

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
          spec "viewport_y" Scalar
          spec "color_normalize" Scalar ]

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
            | "toolbar" -> parts.[parts.Length - 1]
            | _ when parts.Length > 1 -> parts.[parts.Length - 1]
            | _ -> blockName
