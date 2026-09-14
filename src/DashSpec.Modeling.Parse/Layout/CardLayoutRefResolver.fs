namespace DashSpec.Modeling.Parse.Layout

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Card

module CardLayoutRefResolver =

    let resolve (token: string) (cards: IReadOnlyList<CardDefinition>) (context: string) =
        if String.IsNullOrWhiteSpace token then
            invalidArg "token" "Layout token is required."

        let mutable byRef: string option = None
        let mutable byId: string option = None

        for card in cards do
            if card.LayoutRef.IsSome
               && not (String.IsNullOrWhiteSpace card.LayoutRef.Value)
               && String.Equals(card.LayoutRef.Value, token, StringComparison.OrdinalIgnoreCase) then
                if byRef.IsSome then
                    raise (DashSpecParseException($"{context}: layout token '{token}' matches more than one card ref."))
                byRef <- Some card.Id

            if String.Equals(card.Id, token, StringComparison.OrdinalIgnoreCase) then
                if byId.IsSome then
                    raise (DashSpecParseException($"{context}: layout token '{token}' matches more than one card id."))
                byId <- Some card.Id

        match byRef, byId with
        | Some refId, Some id when not (String.Equals(refId, id, StringComparison.OrdinalIgnoreCase)) ->
            raise (DashSpecParseException($"{context}: layout token '{token}' is ambiguous (matches both ref and id)."))
        | _ -> ()

        match byRef, byId with
        | Some refId, _ -> refId
        | None, Some id -> id
        | None, None ->
            raise (DashSpecParseException($"{context}: layout token '{token}' does not match any card ref or id on the tab."))
