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

    let toSchema (catalog: TypeCatalog) (def: RowTypeDef) =
        match TypeCatalog.flattenRowType catalog def.Name with
        | Result.Error message -> raise (DashSpecParseException(message))
        | Result.Ok fields ->
            let rowFields =
                fields
                |> List.map (fun field -> RowFieldSchema(field.Path, toKind field.Kind, field.Optional))
                :> IReadOnlyList<_>

            RowTypeSchema(def.Name, rowFields)

    let toCatalog (defs: RowTypeDef seq) =
        let catalog = TypeCatalog.ofDefinitions defs

        match TypeCatalog.validate catalog with
        | Result.Error errors -> raise (DashSpecParseException(String.Join("; ", errors)))
        | Result.Ok () -> ()

        let map = Dictionary<string, RowTypeSchema>(StringComparer.OrdinalIgnoreCase)

        for def in defs do
            map.[def.Name] <- toSchema catalog def

        map :> IReadOnlyDictionary<_, _>
