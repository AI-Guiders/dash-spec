namespace DashSpec.Modeling.Parse.Palette

open System
open System.Collections.Generic
open DashSpec.Modeling.Core

/// Resolves palette color operands (hex strings, CSS names, const refs) to #rrggbb.
module PaletteColorResolver =

    let private cssColors =
        dict
            [ "aliceblue", "#f0f8ff"
              "antiquewhite", "#faebd7"
              "aqua", "#00ffff"
              "aquamarine", "#7fffd4"
              "azure", "#f0ffff"
              "beige", "#f5f5dc"
              "bisque", "#ffe4c4"
              "black", "#000000"
              "blue", "#0000ff"
              "brown", "#a52a2a"
              "cyan", "#00ffff"
              "gold", "#ffd700"
              "gray", "#808080"
              "green", "#008000"
              "grey", "#808080"
              "indigo", "#4b0082"
              "lime", "#00ff00"
              "magenta", "#ff00ff"
              "maroon", "#800000"
              "navy", "#000080"
              "olive", "#808000"
              "orange", "#ffa500"
              "pink", "#ffc0cb"
              "purple", "#800080"
              "red", "#ff0000"
              "silver", "#c0c0c0"
              "slate", "#708090"
              "teal", "#008080"
              "violet", "#ee82ee"
              "white", "#ffffff"
              "yellow", "#ffff00" ]

    let private tryResolveHexLiteral (raw: string) =
        let value = raw.Trim()
        if not (value.StartsWith '#') then None
        else
            let digits = value.[1..]
            if digits.Length <> 3 && digits.Length <> 6 then
                raise (DashSpecParseException($"Invalid hex color '{raw}'."))
            elif not (digits |> Seq.forall Uri.IsHexDigit) then
                raise (DashSpecParseException($"Invalid hex color '{raw}'."))
            else
                let hex =
                    if digits.Length = 3 then
                        $"#{digits.[0]}{digits.[0]}{digits.[1]}{digits.[1]}{digits.[2]}{digits.[2]}"
                    else
                        $"#{digits}".ToLowerInvariant()
                Some hex

    let resolveOperand (raw: string) (constants: IReadOnlyDictionary<string, string>) (context: string) =
        if String.IsNullOrWhiteSpace raw then invalidArg "raw" "Color operand is required."
        if String.IsNullOrWhiteSpace context then invalidArg "context" "Context is required."

        match tryResolveHexLiteral raw with
        | Some hex -> hex
        | None ->
            match constants.TryGetValue raw with
            | true, constantHex -> constantHex
            | false, _ ->
                match cssColors.TryGetValue raw with
                | true, cssHex -> cssHex
                | false, _ ->
                    raise (DashSpecParseException($"Unknown color '{raw}' in {context}. Use \"#rrggbb\", a CSS color name, or a const reference."))

    let joinColorList (resolvedHex: seq<string>) = String.Join(',', resolvedHex)
