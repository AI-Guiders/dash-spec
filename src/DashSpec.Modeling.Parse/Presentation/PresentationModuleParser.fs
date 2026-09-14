namespace DashSpec.Modeling.Parse.Presentation

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

module PresentationModuleParser =

    let private isChartChromeIncludeKind kind =
        String.Equals(kind, "presentation", StringComparison.OrdinalIgnoreCase)
        || String.Equals(kind, "chrome", StringComparison.OrdinalIgnoreCase)

    let private readIncludeReference (reader: TokenReader) =
        let kind = reader.ReadIdent()
        let reference = reader.ReadString()
        kind, reference

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
        reader.Expect TokenKind.At
        reader.ExpectKeyword "presentation"
        let id = reader.ReadIdent()
        if String.IsNullOrWhiteSpace id then
            raise (DashSpecParseException("@presentation module requires @presentation <id>."))
        reader.SkipNewlines()

        let includes = ResizeArray<PresentationInclude>()
        let mutable continueIncludes = true
        while continueIncludes && not reader.IsEof do
            reader.SkipNewlines()
            if reader.IsEof then
                continueIncludes <- false
            elif reader.TryKeyword "include" then
                let kind, reference = readIncludeReference reader
                if not (isChartChromeIncludeKind kind) then
                    raise (DashSpecParseException($"@presentation module only supports include presentation/chrome, got '{kind}'."))
                includes.Add({ Kind = kind; Reference = reference })
                reader.SkipNewlines()
            else
                continueIncludes <- false

        let local = parseLocalBlock reader
        if local.IsNone && includes.Count = 0 then
            raise (DashSpecParseException("@presentation module requires at least one property."))

        { Id = id; Includes = includes :> IReadOnlyList<_>; Local = local }

    let parsePresentationModuleWithId (text: string) =
        let doc = parsePresentationModule text
        doc.Id, doc
