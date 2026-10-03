namespace DashSpec.Modeling.Core

open System
open System.Collections.Generic

module TypeInvariantLaws =

    let private tryGet (definitions: IReadOnlyDictionary<string, RowTypeDef>) (typeName: string) =
        match definitions.TryGetValue typeName with
        | true, def -> Some def
        | false, _ -> None

    let private fieldNamed (def: RowTypeDef) (name: string) =
        def.Fields |> Array.tryFind (fun field -> String.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase))

    let private isNamed (typeName: string) (field: RowFieldDef) =
        match field.Type with
        | DashType.Named name -> String.Equals(name, typeName, StringComparison.OrdinalIgnoreCase)
        | _ -> false

    /// <summary>ADR-0079: <c>DateTime</c> must compose <c>Date Day</c> and <c>Time Clock</c>.</summary>
    let validate (definitions: IReadOnlyDictionary<string, RowTypeDef>) =
        match tryGet definitions "DateTime" with
        | None -> []
        | Some dateTime ->
            let errors = ResizeArray<string>()

            match fieldNamed dateTime "Day" with
            | None -> errors.Add("DateTime: missing field 'Day'.")
            | Some day when not (isNamed "Date" day) -> errors.Add("DateTime.Day must be type Date.")
            | _ -> ()

            match fieldNamed dateTime "Clock" with
            | None -> errors.Add("DateTime: missing field 'Clock'.")
            | Some clock when not (isNamed "Time" clock) -> errors.Add("DateTime.Clock must be type Time.")
            | _ -> ()

            errors |> Seq.toList
