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

    let private tryProjectTransformFromAst (root: DashSpec.Modeling.Parse.Syntax.DashSpecAstNode) : SeriesTransformBlock option =
        match
            DashSpec.Modeling.Parse.Syntax.DashSpecAst.descendants root
            |> Seq.tryPick (function
                | DashSpec.Modeling.Parse.Syntax.DashSpecAstNode.ModuleDeclaration m as node ->
                    match m.Directive with
                    | DashSpec.Modeling.Parse.Syntax.DashSpecModuleDirective.Unit("transform", _id) -> Some node
                    | _ -> None
                | _ -> None)
        with
        | None -> None
        | Some moduleNode ->
            let propMembers =
                let moduleMembers = DashSpec.Modeling.Parse.Syntax.DashSpecAst.members moduleNode
                let seriesBlock =
                    moduleMembers
                    |> Array.tryPick (function
                        | DashSpec.Modeling.Parse.Syntax.DashSpecAstNode.BlockDeclaration b ->
                            match b.Opener with
                            | DashSpec.Modeling.Parse.Syntax.DashSpecBlockOpener.Anonymous DashSpec.Modeling.Parse.Syntax.DashSpecBlockKeyword.Series ->
                                Some b.Members
                            | _ -> None
                        | _ -> None)

                defaultArg seriesBlock moduleMembers

            match PropertyBlockParser.tryProjectPropertiesFromAst propMembers PropertyBlockParser.seriesTransformSchema false with
            | Some props when props.Count > 0 -> Some(toBlock props)
            | _ -> None

    let parseTransformFile (text: string) : SeriesTransformBlock =
        if String.IsNullOrWhiteSpace text then invalidArg "text" "Transform text is required."

        let tree = DashSpec.Modeling.Parse.Syntax.DashSpecSurfaceSyntax.parse text
        match tryProjectTransformFromAst tree.Root with
        | Some block -> block
        | None ->
        let reader = ParserUtilities.createReader text
        reader.SkipFileDirectives()
        reader.ExpectKeyword "module"
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
