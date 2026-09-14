namespace DashSpec.Modeling.Parse.Phrase

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse

module PhraseTemplateMatcher =

    type private PatternPart =
        { IsSlot: bool
          Literal: string option
          SlotName: string option
          SlotKind: PhraseSlotKind
          Optional: bool }

    let private tryCoerceToken (token: PhraseToken) (kind: PhraseSlotKind) =
        match kind with
        | PhraseSlotKind.String ->
            token.Kind = PhraseTokenKind.String || token.Kind = PhraseTokenKind.Ident
        | PhraseSlotKind.Int ->
            token.Kind = PhraseTokenKind.Int
            || (token.Kind = PhraseTokenKind.Ident
                && let mutable parsed = 0 in Int32.TryParse(token.Value, &parsed))
        | _ -> token.Kind = PhraseTokenKind.Ident || token.Kind = PhraseTokenKind.String

    let private addLiterals (parts: ResizeArray<PatternPart>) (literalSegment: string) =
        for literal in literalSegment.Split(' ', StringSplitOptions.RemoveEmptyEntries) do
            parts.Add
                { IsSlot = false
                  Literal = Some literal
                  SlotName = None
                  SlotKind = PhraseSlotKind.Ident
                  Optional = false }

    let private compilePattern (pattern: string) (slots: IDictionary<string, PhraseSlotDescriptor>) =
        let parts = ResizeArray<PatternPart>()
        let mutable index = 0

        while index < pattern.Length do
            let slotStart = pattern.IndexOf('{', index)
            if slotStart < 0 then
                addLiterals parts (pattern.Substring index)
                index <- pattern.Length
            else
                addLiterals parts (pattern.Substring(index, slotStart - index))
                let slotEnd = pattern.IndexOf('}', slotStart + 1)
                if slotEnd < 0 then
                    raise (DashSpecParseException($"Invalid phrase template pattern: unclosed '{{' in '{pattern}'."))

                let mutable slotName = pattern.Substring(slotStart + 1, slotEnd - slotStart - 1)
                let mutable optional = false
                if slotName.EndsWith('?') then
                    optional <- true
                    slotName <- slotName.Substring(0, slotName.Length - 1)

                let slot =
                    match slots.TryGetValue slotName with
                    | true, descriptor -> descriptor
                    | false, _ -> { Name = slotName; Kind = PhraseSlotKind.Ident; Optional = false }

                parts.Add
                    { IsSlot = true
                      Literal = None
                      SlotName = Some slotName
                      SlotKind = slot.Kind
                      Optional = optional || slot.Optional }
                index <- slotEnd + 1

        parts :> IReadOnlyList<_>

    let tryMatch (tokens: IReadOnlyList<PhraseToken>) (template: PhraseTemplateDescriptor) =
        let map = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        let slotIndex =
            template.Slots
            |> Seq.map (fun s -> s.Name, s)
            |> dict
        let parts = compilePattern template.Pattern slotIndex
        let mutable index = 0
        let mutable failed = false

        for part in parts do
            if not failed then
                if part.IsSlot then
                    if index >= tokens.Count then
                        if not part.Optional then failed <- true
                    else
                        let token = tokens.[index]
                        if not (tryCoerceToken token part.SlotKind) then failed <- true
                        else
                            map.[part.SlotName.Value] <- token.Value
                            index <- index + 1
                elif index >= tokens.Count
                     || not (String.Equals(tokens.[index].Value, part.Literal.Value, StringComparison.OrdinalIgnoreCase)) then
                    failed <- true
                else
                    index <- index + 1

        if failed || index <> tokens.Count then None
        else Some(template.HandlerId, map :> IReadOnlyDictionary<_, _>)

    let tryMatchAny (tokens: IReadOnlyList<PhraseToken>) (templates: IReadOnlyList<PhraseTemplateDescriptor>) =
        templates
        |> Seq.tryPick (fun template ->
            match tryMatch tokens template with
            | Some(handlerId, args) -> Some(handlerId, args)
            | None -> None)
