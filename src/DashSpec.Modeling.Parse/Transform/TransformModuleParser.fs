namespace DashSpec.Modeling.Parse.Transform

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Syntax

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

        let tree = DashSpecSurfaceSyntax.parse text
        let env = UnitGrammar.unwrap DashSpecModuleKind.Transform tree.Root

        for b in env.View.Blocks do
            if b.Opener <> DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Series then
                let headerText = b.Tokens |> Array.map (fun t -> t.Text) |> String.concat " "
                raise (DashSpecParseException($"Expected 'transform series', got '{headerText}'."))

        let propMembers = UnitGrammar.unwrapOptionalBlock DashSpecBlockKeyword.Series env.Body
        let props =
            PropertyBlockParser.projectPropertiesFromAst
                propMembers
                PropertyBlockParser.seriesTransformSchema
                "transform series"
                false

        toBlock props
