namespace DashSpec.Modeling.Parse.Types

open System
open System.Collections.Generic
open DashSpec.Abstractions.Data
open DashSpec.Modeling.Core

module RowTypeSchemaBridge =

    let private toKind (primitive: DashPrimitive) =
        match primitive with
        | DashPrimitive.Bool -> DashPrimitiveKind.Bool
        | DashPrimitive.Int -> DashPrimitiveKind.Int
        | DashPrimitive.Decimal -> DashPrimitiveKind.Decimal
        | DashPrimitive.String -> DashPrimitiveKind.String
        | DashPrimitive.Duration -> DashPrimitiveKind.Duration
        | DashPrimitive.Date -> DashPrimitiveKind.Date
        | DashPrimitive.Time -> DashPrimitiveKind.Time
        | DashPrimitive.DateTime -> DashPrimitiveKind.DateTime

    let toSchema (def: RowTypeDef) =
        let fields =
            def.Fields
            |> Array.map (fun field -> RowFieldSchema(field.Name, toKind field.Kind, field.Optional))
            :> IReadOnlyList<_>

        RowTypeSchema(def.Name, fields)

    let toCatalog (defs: RowTypeDef seq) =
        let map = Dictionary<string, RowTypeSchema>(StringComparer.OrdinalIgnoreCase)

        for def in defs do
            if map.ContainsKey def.Name then
                raise (DashSpecParseException($"Duplicate row type '{def.Name}'."))

            map.[def.Name] <- toSchema def

        map :> IReadOnlyDictionary<_, _>
