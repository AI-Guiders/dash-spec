namespace DashSpec.Modeling.Core

open System
open System.Collections.Generic

/// <summary>Nominal UDT registry with composition validation (ADR-0079).</summary>
type TypeCatalog =
    { Definitions: IReadOnlyDictionary<string, RowTypeDef> }

[<RequireQualifiedAccess>]
type TypePathSegment = Field of name: string

type FlatRowField =
    { Path: string
      Kind: DashPrimitive
      Optional: bool }

module TypeCatalog =

    let empty =
        { Definitions = Map.empty :> IReadOnlyDictionary<_, _> }

    let ofDefinitions (defs: seq<RowTypeDef>) =
        let map = Dictionary<string, RowTypeDef>(StringComparer.OrdinalIgnoreCase)

        for def in defs do
            if String.IsNullOrWhiteSpace def.Name then
                invalidArg "defs" "Type name is required."

            if map.ContainsKey def.Name then
                raise (DashSpecParseException($"Duplicate type '{def.Name}'."))

            map.[def.Name] <- def

        { Definitions = map :> IReadOnlyDictionary<_, _> }

    let tryGet (catalog: TypeCatalog) (typeName: string) =
        match catalog.Definitions.TryGetValue typeName with
        | true, def -> Some def
        | false, _ -> None

    let rec private visitType
        (catalog: TypeCatalog)
        (visiting: HashSet<string>)
        (typeName: string)
        (fieldPath: string)
        (optional: bool)
        (dashType: DashType)
        (acc: FlatRowField list)
        =
        match dashType with
        | DashType.Primitive kind ->
            { Path = fieldPath; Kind = kind; Optional = optional } :: acc

        | DashType.FixedArray(element, length) ->
            let mutable fields = acc

            for index in 0 .. length - 1 do
                let segment = $"{fieldPath}_{index}"

                fields <-
                    { Path = segment
                      Kind = element
                      Optional = optional }
                    :: fields

            fields

        | DashType.Named nested ->
            if visiting.Contains nested then
                raise (DashSpecParseException($"Cyclic type reference involving '{nested}'."))

            match tryGet catalog nested with
            | None -> raise (DashSpecParseException($"Unknown type '{nested}' (referenced from '{typeName}')."))
            | Some nestedDef ->
                visiting.Add nested |> ignore

                let mutable fields = acc

                for field in nestedDef.Fields do
                    let childPath =
                        if String.IsNullOrEmpty fieldPath then field.Name
                        else $"{fieldPath}.{field.Name}"

                    fields <-
                        visitType catalog visiting nested childPath field.Optional field.Type fields

                visiting.Remove nested |> ignore
                fields

    /// <summary>Flatten nested UDT to primitive leaf paths for SQL wire (ADR-0087 until nested cells).</summary>
    let flattenRowType (catalog: TypeCatalog) (typeName: string) =
        match tryGet catalog typeName with
        | None -> Result.Error $"Row type '{typeName}' is not defined."
        | Some def ->
            let visiting = HashSet<string>(StringComparer.OrdinalIgnoreCase)
            visiting.Add def.Name |> ignore
            let fields = List<FlatRowField>()

            for field in def.Fields do
                visitType catalog visiting def.Name field.Name field.Optional field.Type []
                |> List.iter (fun flat -> fields.Add flat)

            if fields.Count = 0 then
                Result.Error $"Type '{typeName}' has no primitive fields."
            else
                Result.Ok(fields |> Seq.toList)

    let validate (catalog: TypeCatalog) =
        let errors = ResizeArray<string>()

        for KeyValue(typeName, _) in catalog.Definitions do
            try
                match flattenRowType catalog typeName with
                | Result.Ok _ -> ()
                | Result.Error message -> errors.Add($"{typeName}: {message}")
            with :? DashSpecParseException as ex ->
                errors.Add($"{typeName}: {ex.Message}")

        if errors.Count = 0 then Result.Ok() else Result.Error(errors |> Seq.toList)

    /// <summary>Resolve a field path relative to a row type (ADR-0079 field paths).</summary>
    let tryResolveFieldPath (catalog: TypeCatalog) (rowTypeName: string) (path: string) =
        if String.IsNullOrWhiteSpace path then
            None
        else
            let segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries)

            let rec walk (currentType: string) (index: int) =
                if index >= segments.Length then
                    Some currentType
                else
                    match tryGet catalog currentType with
                    | None -> None
                    | Some def ->
                        let segment = segments.[index]

                        match def.Fields |> Array.tryFind (fun f -> String.Equals(f.Name, segment, StringComparison.OrdinalIgnoreCase)) with
                        | None -> None
                        | Some field ->
                            match field.Type with
                            | DashType.Named next -> walk next (index + 1)
                            | DashType.Primitive _
                            | DashType.FixedArray _ ->
                                if index = segments.Length - 1 then Some currentType else None

            walk rowTypeName 0

    let portTypesCompatible (left: DashPortType) (right: DashPortType) =
        match left, right with
        | DashPortType.Rows a, DashPortType.Rows b -> String.Equals(a, b, StringComparison.OrdinalIgnoreCase)
