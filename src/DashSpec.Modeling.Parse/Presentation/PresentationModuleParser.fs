namespace DashSpec.Modeling.Parse.Presentation

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

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

    let private parseLocalBlock (reader: TokenReader) =
        if reader.IsEof then None
        elif reader.TryKeyword "presentation" then
            let props =
                PropertyBlockParser.parse
                    reader
                    PropertySchemas.presentation
                    "presentation"
                    false
                    false
            if props.Count = 0 then None else Some(toBlock props)
        else
            let props =
                PropertyBlockParser.parseFlatProperties
                    reader
                    PropertySchemas.presentation
                    "@presentation module"
                    false
                    false
            if props.Count = 0 then None else Some(toBlock props)

    let parsePresentationModule (text: string) : PresentationModuleDocument =
        if String.IsNullOrWhiteSpace text then invalidArg "text" "Presentation text is required."
        let reader = ParserUtilities.createReader text
        reader.SkipFileDirectives()
        reader.ExpectKeyword "module"
        reader.ExpectKeyword "presentation"
        let id = reader.ReadIdent()
        if String.IsNullOrWhiteSpace id then
            raise (DashSpecParseException("@presentation module requires @presentation <id>."))
        reader.SkipNewlines()

        let local = parseLocalBlock reader
        if local.IsNone then
            raise (DashSpecParseException("@presentation module requires at least one property."))

        { Id = id; Local = local }

    let parsePresentationModuleWithId (text: string) =
        let doc = parsePresentationModule text
        doc.Id, doc
