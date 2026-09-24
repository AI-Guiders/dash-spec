namespace DashSpec.Modeling.Parse.Document

open System
open System.Collections.Generic

type FilterScopeDefaults = Dictionary<string, Dictionary<string, string>>

[<RequireQualifiedAccess>]
module FilterScopeDefaults =

    let create () =
        Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)

    let set (scope: FilterScopeDefaults) (filterName: string) (property: string) (value: string) =
        let mutable filterProps = Unchecked.defaultof<Dictionary<string, string>>

        if not (scope.TryGetValue(filterName, &filterProps)) then
            filterProps <- Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            scope.[filterName] <- filterProps

        filterProps.[property] <- value

    let tryGet (scope: FilterScopeDefaults) (filterName: string) (property: string) =
        match scope.TryGetValue filterName with
        | true, props ->
            match props.TryGetValue property with
            | true, value -> Some value
            | false, _ -> None
        | false, _ -> None

    let merge (target: FilterScopeDefaults) (source: FilterScopeDefaults) =
        for filterEntry in source do
            for propEntry in filterEntry.Value do
                set target filterEntry.Key propEntry.Key propEntry.Value

    let resolveProperty
        (scopes: FilterScopeDefaults seq)
        (filterName: string)
        (property: string)
        =
        scopes
        |> Seq.tryPick (fun scope -> tryGet scope filterName property)

    let toReadOnly (scope: FilterScopeDefaults) =
        if scope.Count = 0 then
            None
        else
            let mapped =
                Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)

            for filterEntry in scope do
                mapped.[filterEntry.Key] <- filterEntry.Value :> IReadOnlyDictionary<string, string>

            Some(mapped :> IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>)
