namespace DashSpec.Modeling.Parse

open System

/// Property schemas aligned with Core.Parsing.PropertySchemas (ADR-0048 M5).
module PropertySchemas =

    type PropertyValueType =
        | Scalar
        | String

    type PropertySpec = { Name: string; ValueType: PropertyValueType }

    let seriesTransform =
        [ { Name = "use"; ValueType = Scalar }
          { Name = "max"; ValueType = Scalar }
          { Name = "other"; ValueType = String } ]

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
