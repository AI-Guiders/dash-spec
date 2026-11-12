namespace DashSpec.Modeling.Parse.Tooltip

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing
open DashSpec.Modeling.Parse.Syntax

module TooltipModuleParser =

    let private unquote (raw: string) =
        let t = raw.Trim()
        if t.Length >= 2 && t.[0] = '"' && t.[t.Length - 1] = '"' then
            t.Substring(1, t.Length - 2)
        else
            t

    /// Project a tooltip definition from any container's AST members (shared by `module tooltip <id>` and inline `tooltip <id>`).
    let projectFromMembers (id: string) (members: DashSpecAstNode[]) : TooltipDefinition =
        let view = UnitGrammar.view members
        let mutable variables: IReadOnlyDictionary<string, string> option = None
        let mutable template: string option = None
        let mutable source: string option = None

        for node in view.Body do
            match node with
            | DashSpecAstNode.BlockDeclaration b ->
                match b.Opener with
                | DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Variables ->
                    if variables.IsSome then
                        raise (DashSpecParseException($"Tooltip '{id}': duplicate variables block."))

                    let vars = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    let innerView = UnitGrammar.view b.Members

                    if innerView.Body.Length <> innerView.Properties.Length then
                        raise (DashSpecParseException($"Tooltip '{id}' variables expects 'key = column' assignments."))

                    for prop in innerView.Properties do
                        if prop.ValueTokens.Length <> 1 || prop.ValueTokens.[0].LexKind <> TokenKind.Ident then
                            raise (DashSpecParseException($"Tooltip '{id}' variables: '{prop.Key}' requires an identifier column."))
                        vars.[prop.Key] <- prop.ValueTokens.[0].Text

                    if vars.Count = 0 then
                        raise (DashSpecParseException($"Tooltip '{id}' variables requires at least one entry."))

                    variables <- Some(vars :> IReadOnlyDictionary<string, string>)
                | _ ->
                    let headerText = b.Tokens |> Array.map (fun t -> t.Text) |> String.concat " "
                    raise (DashSpecParseException($"Unexpected block '{headerText}' in tooltip '{id}'."))
            | DashSpecAstNode.PropertyAssignment p ->
                if String.Equals(p.Key, "tooltip", StringComparison.OrdinalIgnoreCase) then
                    if p.ValueTokens.Length <> 1 || p.ValueTokens.[0].LexKind <> TokenKind.String then
                        raise (DashSpecParseException($"Tooltip '{id}': tooltip requires a string literal."))
                    template <- Some(unquote p.ValueTokens.[0].Text)
                elif String.Equals(p.Key, "source", StringComparison.OrdinalIgnoreCase) then
                    if p.ValueTokens.Length <> 1 || p.ValueTokens.[0].LexKind <> TokenKind.Ident then
                        raise (DashSpecParseException($"Tooltip '{id}': source requires a column name."))
                    let col = p.ValueTokens.[0].Text
                    if String.IsNullOrWhiteSpace col then
                        raise (DashSpecParseException($"Tooltip '{id}': source requires a column name."))
                    source <- Some col
                else
                    raise (DashSpecParseException($"Tooltip '{id}': unexpected property '{p.Key}'."))
            | other ->
                let text = DashSpecAst.tokens other |> Array.map (fun t -> t.Text) |> String.concat " "
                raise (DashSpecParseException($"Unexpected statement '{text}' in tooltip '{id}'."))

        let resolvedVariables, resolvedTemplate =
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

        { Id = id; Variables = resolvedVariables; Template = resolvedTemplate }

    let parseTooltipFile (text: string) : TooltipDefinition =
        if String.IsNullOrWhiteSpace text then invalidArg "text" "Tooltip text is required."
        let tree = DashSpecSurfaceSyntax.parse text
        let env = UnitGrammar.unwrap DashSpecModuleKind.Tooltip tree.Root
        projectFromMembers env.Id env.Body

    let parseTooltipFileWithId (text: string) =
        let def = parseTooltipFile text
        def.Id, def

    /// <summary>Parse inline tooltip body embedded in @card / @diagram (ADR-0048).</summary>
    let parseTooltipInlineBody (id: string) (bodyText: string) : TooltipDefinition =
        if String.IsNullOrWhiteSpace id then invalidArg "id" "Tooltip id is required."
        let tree = DashSpecSurfaceSyntax.parse bodyText
        projectFromMembers id (DashSpecAst.members tree.Root)

    /// <summary>Parse inline tooltip from current reader position (@diagram / @card).</summary>
    let parseInline (reader: TokenReader) (id: string) : TooltipDefinition =
        if String.IsNullOrWhiteSpace id then invalidArg "id" "Tooltip id is required."
        let mutable variables: IReadOnlyDictionary<string, string> option = None
        let mutable template: string option = None
        let mutable source: string option = None
        let mutable finished = false

        while not finished && not reader.IsEof do
            reader.SkipNewlines()
            if reader.IsEof then ()
            elif reader.TryKeyword "variables" then
                if variables.IsSome then
                    raise (DashSpecParseException($"Tooltip '{id}': duplicate variables block."))
                let vars = MemberGrammar.parseIdentMapBlock reader "variables" $"Tooltip '{id}' variables" :> IReadOnlyDictionary<string, string>
                variables <- Some vars
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
                finished <- true
            else
                raise (reader.Unexpected())

        let resolvedVariables, resolvedTemplate =
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

        { Id = id; Variables = resolvedVariables; Template = resolvedTemplate }
