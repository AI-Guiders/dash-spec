namespace DashSpec.Modeling.Parse.Tooltip

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

module TooltipModuleParser =

    let private parseVariables (reader: TokenReader) (tooltipId: string) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let map = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

        while not (BlockSyntax.isBlockEnd reader "variables" None) && not reader.IsEof do
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader "variables" None then ()
            else
                let name = reader.ReadIdent()
                if String.IsNullOrWhiteSpace name then
                    raise (DashSpecParseException($"Tooltip '{tooltipId}': variables entry requires a name."))
                reader.Expect TokenKind.Eq
                let column = reader.ReadIdent()
                if String.IsNullOrWhiteSpace column then
                    raise (DashSpecParseException($"Tooltip '{tooltipId}': variables '{name}' requires a column."))
                if not (map.TryAdd(name, column)) then
                    raise (DashSpecParseException($"Tooltip '{tooltipId}': duplicate variable '{name}'."))
                reader.SkipNewlines()

        BlockSyntax.expectBlockEnd reader "variables" None
        map :> IReadOnlyDictionary<string, string>

    let private parseBody (reader: TokenReader) (id: string) =
        let mutable variables: IReadOnlyDictionary<string, string> option = None
        let mutable template: string option = None
        let mutable source: string option = None

        while not reader.IsEof do
            reader.SkipNewlines()
            if reader.IsEof then ()
            elif reader.TryKeyword "variables" then
                if variables.IsSome then
                    raise (DashSpecParseException($"Tooltip '{id}': duplicate variables block."))
                variables <- Some(parseVariables reader id)
            elif reader.TryKeyword "tooltip" then
                reader.Expect TokenKind.Eq
                template <- Some(reader.ReadString())
            elif reader.TryKeyword "source" then
                reader.Expect TokenKind.Eq
                let col = reader.ReadIdent()
                if String.IsNullOrWhiteSpace col then
                    raise (DashSpecParseException($"Tooltip '{id}': source requires a column name."))
                source <- Some col
            elif reader.TryKeyword "end" && reader.TryKeyword "tooltip" then
                ()
            elif not reader.IsEof then
                raise (reader.Unexpected())
            else
                ()

        let variables, template =
            match source with
            | Some col when variables.IsSome || template.IsSome ->
                raise (DashSpecParseException($"Tooltip '{id}': source cannot be combined with variables/tooltip."))
            | Some col ->
                let vars = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                vars.["value"] <- col
                vars :> IReadOnlyDictionary<string, string>, "{value}"
            | None ->
                match variables, template with
                | Some v, Some t -> v, t
                | _ ->
                    if variables.IsNone || variables.Value.Count = 0 then
                        raise (DashSpecParseException($"Tooltip '{id}': variables (or source) is required."))
                    if template.IsNone || String.IsNullOrWhiteSpace template.Value then
                        raise (DashSpecParseException($"Tooltip '{id}': tooltip = \"...\" is required."))
                    variables.Value, template.Value

        { Id = id; Variables = variables; Template = template }

    let parseTooltipFile (text: string) : TooltipDefinition =
        if String.IsNullOrWhiteSpace text then invalidArg "text" "Tooltip text is required."
        let reader = ParserUtilities.createReader text
        reader.SkipFileDirectives()
        reader.Expect TokenKind.At
        reader.ExpectKeyword "tooltip"
        let id = reader.ReadIdent()
        if String.IsNullOrWhiteSpace id then
            raise (DashSpecParseException("@tooltip module requires @tooltip <id>."))
        reader.SkipNewlines()
        parseBody reader id

    let parseTooltipFileWithId (text: string) =
        let def = parseTooltipFile text
        def.Id, def

    /// <summary>Parse inline tooltip body embedded in @card / @diagram (ADR-0048).</summary>
    let parseTooltipInlineBody (id: string) (bodyText: string) : TooltipDefinition =
        if String.IsNullOrWhiteSpace id then invalidArg "id" "Tooltip id is required."
        let reader = ParserUtilities.createReader bodyText
        reader.SkipNewlines()
        parseBody reader id