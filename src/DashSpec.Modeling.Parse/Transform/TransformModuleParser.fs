namespace DashSpec.Modeling.Parse.Transform

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

module TransformModuleParser =

    let private toBlock (props: System.Collections.Generic.Dictionary<string, string>) =
        if props.Count = 0 then
            raise (DashSpecParseException("@transform module requires at least one property."))

        let usePreset =
            match props.TryGetValue "use" with
            | true, value -> Some value
            | false, _ -> None

        let max =
            match props.TryGetValue "max" with
            | true, raw ->
                match Int32.TryParse raw with
                | true, parsed when parsed > 0 -> Some parsed
                | _ -> None
            | false, _ -> None

        let other =
            match props.TryGetValue "other" with
            | true, value -> Some value
            | false, _ -> None

        { UsePreset = usePreset; Max = max; OtherLabel = other }

    let parseTransformFile (text: string) : SeriesTransformBlock =
        if String.IsNullOrWhiteSpace text then invalidArg "text" "Transform text is required."

        let reader = ParserUtilities.createReader text
        reader.SkipFileDirectives()
        reader.Expect TokenKind.At
        reader.ExpectKeyword "transform"
        reader.ReadIdent() |> ignore
        reader.SkipNewlines()

        let props =
            if reader.TryKeyword "transform" then
                if not (reader.TryKeyword "series") then
                    raise (DashSpecParseException("Expected 'series' after transform."))

                PropertyBlockParser.parse
                    reader
                    PropertyBlockParser.seriesTransformSchema
                    "transform series"
                    false
                    false
            else
                PropertyBlockParser.parseFlatProperties
                    reader
                    PropertyBlockParser.seriesTransformSchema
                    "@transform module"
                    false
                    false

        toBlock props
