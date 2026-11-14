namespace DashSpec.Modeling.Parse.Presentation

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Syntax

[<CLIMutable>]
type PresentationBlock =
    { UsePreset: string option
      Properties: IReadOnlyDictionary<string, string> }

[<CLIMutable>]
type PresentationModuleDocument =
    { Id: string
      Local: PresentationBlock option }

module PresentationModuleParser =

    let private toBlock (props: IDictionary<string, string>) =
        let usePreset =
            match props.TryGetValue "use" with
            | true, value -> Some value
            | false, _ -> None
        let inlineProps =
            let map = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            for kv in props do
                if not (kv.Key.Equals("use", StringComparison.OrdinalIgnoreCase)) then
                    map.[kv.Key] <- kv.Value
            map :> IReadOnlyDictionary<string, string>
        { UsePreset = usePreset; Properties = inlineProps }

    let parseEnvelope (env: UnitGrammar.UnitEnvelope) : PresentationModuleDocument =
        let propMembers = UnitGrammar.unwrapOptionalBlock DashSpecBlockKeyword.Presentation env.Body

        let props =
            PropertyBlockParser.projectPropertiesFromAst
                propMembers
                (PropertySchemas.presentation ())
                "presentation"
                false

        if props.Count = 0 then
            raise (DashSpecParseException("@presentation module requires at least one property."))

        { Id = env.Id; Local = Some(toBlock props) }

    let parsePresentationModule (text: string) : PresentationModuleDocument =
        UnitGrammar.parseUnit DashSpecModuleKind.Presentation parseEnvelope text

    let parsePresentationModuleWithId (text: string) =
        let doc = parsePresentationModule text
        doc.Id, doc
