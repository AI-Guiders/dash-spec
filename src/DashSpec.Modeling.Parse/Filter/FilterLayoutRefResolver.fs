namespace DashSpec.Modeling.Parse.Filter

open System
open System.Collections.Generic
open DashSpec.Modeling.Core

/// <summary>Resolve layout board tokens to filter names (ref / name, ambiguity checks).</summary>
module FilterLayoutRefResolver =

    let resolve (token: string) (filters: IReadOnlyList<FilterDefinition>) (context: string) =
        if String.IsNullOrWhiteSpace token then
            invalidArg "token" "Layout token is required."

        let mutable byRef: string option = None
        let mutable byName: string option = None

        for filter in filters do
            match filter.LayoutRef with
            | Some layoutRef when String.Equals(layoutRef, token, StringComparison.OrdinalIgnoreCase) ->
                if byRef.IsSome then
                    raise (DashSpecParseException($"{context}: layout token '{token}' matches more than one filter ref."))
                byRef <- Some filter.Name
            | _ -> ()

            if String.Equals(filter.Name, token, StringComparison.OrdinalIgnoreCase) then
                if byName.IsSome then
                    raise (DashSpecParseException($"{context}: layout token '{token}' matches more than one filter name."))
                byName <- Some filter.Name

        match byRef, byName with
        | Some r, Some n when not (String.Equals(r, n, StringComparison.OrdinalIgnoreCase)) ->
            raise (DashSpecParseException($"{context}: layout token '{token}' is ambiguous (matches both ref and name)."))
        | _ ->
            match byRef |> Option.orElse byName with
            | Some name -> name
            | None ->
                raise (DashSpecParseException($"{context}: layout token '{token}' does not match any filter ref or name."))
