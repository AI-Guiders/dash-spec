namespace DashSpec.Modeling.Parse.DataFlow

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Lexing
open DashSpec.Modeling.Parse.Syntax

module TransformStepParser =

    /// Declared param kinds → lexer token kinds ("ident" covers column names,
    /// enum values, slash chains). "string" is universal: every declared kind also
    /// accepts a plain string.
    let private declaredTokenKind =
        function
        | "timeshift" -> Some TokenKind.TimeShift
        | "iana" -> Some TokenKind.IanaZone
        | "string" -> Some TokenKind.String
        | "ident" -> Some TokenKind.Ident
        | _ -> None

    let private unwrapStringLiteral (raw: string) =
        let trimmed = raw.Trim()
        if trimmed.Length >= 2 && trimmed.[0] = '"' && trimmed.[trimmed.Length - 1] = '"' then
            trimmed.Substring(1, trimmed.Length - 2)
        else
            trimmed

    let private validateAndExtractParamValue (pluginId: string) (prop: PropertySyntax) : string option =
        if prop.ValueTokens.Length = 0 then
            None
        else
            let firstKind = prop.ValueTokens.[0].LexKind
            let isSlashChain =
                prop.ValueTokens.Length > 1
                && prop.ValueTokens
                   |> Array.mapi (fun i t -> if i % 2 = 0 then t.LexKind = TokenKind.Ident else t.LexKind = TokenKind.Slash)
                   |> Array.forall id

            let effectiveKind =
                if isSlashChain then Some TokenKind.IanaZone
                elif prop.ValueTokens.Length = 1 then
                    match firstKind with
                    | TokenKind.String
                    | TokenKind.TimeShift
                    | TokenKind.IanaZone
                    | TokenKind.Ident -> Some firstKind
                    | _ -> None
                else
                    None

            match effectiveKind with
            | None -> None
            | Some kind ->
                match TransformDeclarations.requirementFor pluginId prop.Key with
                | Some(kinds, hint) ->
                    let accepts k =
                        k = TokenKind.String
                        || kinds |> List.exists (fun declared -> declaredTokenKind declared = Some k)
                        || (isSlashChain && kinds |> List.exists (fun declared -> declared = "ident" || declared = "iana"))

                    if not (accepts kind) then
                        raise (DashSpecParseException(hint))
                | None -> ()

                if isSlashChain then
                    Some(prop.ValueTokens |> Array.map (fun t -> t.Text) |> String.concat "")
                elif kind = TokenKind.String then
                    Some(unwrapStringLiteral prop.ValueTokens.[0].Text)
                else
                    Some prop.ValueTokens.[0].Text

    /// <summary>ADR-0130: project a `transform use &lt;pluginId&gt; { … }` block directly from
    /// its typed AST `BlockDeclaration` and `PropertyAssignment` members.</summary>
    let projectStepFromAst (transformerId: string) (pluginId: string) (block: BlockDeclarationSyntax) : DashflowTransformStepDef =
        TransformDeclarationParser.ensureStdlibRegistered ()

        if not (TransformDeclarations.isKnown pluginId) then
            let known =
                TransformDeclarations.registered ()
                |> List.map (fun d -> d.Id)
                |> Array.ofList
                |> fun ids -> String.Join("|", ids)

            raise (
                DashSpecParseException(
                    $"transformer '{transformerId}': unknown transform '{pluginId}' — declared: {known}"))

        let hasBrace =
            block.Tokens
            |> Array.exists (fun t -> t.LexKind = TokenKind.LBrace)

        if not hasBrace && block.Members.Length = 0 then
            match TransformDeclarations.tryFind pluginId with
            | Some decl when decl.RequiresParameterBlock ->
                raise (
                    DashSpecParseException(
                        $"transformer '{transformerId}': transform use {pluginId} requires a parameter block."))
            | _ -> { PluginId = pluginId; Parameters = [||] }
        else
            let parameters = ResizeArray<string * string>()

            for memberNode in block.Members do
                match memberNode with
                | DashSpecAstNode.PropertyAssignment prop ->
                    match validateAndExtractParamValue pluginId prop with
                    | Some value -> parameters.Add(prop.Key, value)
                    | None ->
                        raise (DashSpecParseException($"transformer '{transformerId}': invalid parameter '{prop.Key} = {prop.ValueText}' in transform '{pluginId}'."))
                | DashSpecAstNode.BlankLine _
                | DashSpecAstNode.EndBlock _ -> ()
                | memberNode when DashSpecAst.isTrivia memberNode -> ()
                | other ->
                    let lineText = DashSpecAst.tokens other |> Array.map (fun t -> t.Text) |> String.concat " "
                    raise (DashSpecParseException($"transformer '{transformerId}': unexpected statement '{lineText}' in transform '{pluginId}'."))

            let normalized =
                BuiltinScalarTransforms.normalizeStep transformerId pluginId (parameters.ToArray())
                |> Array.map (fun (k, v) -> { Key = k; Value = v })

            { PluginId = pluginId; Parameters = normalized }

    /// Compatibility wrapper returning `Some` over `projectStepFromAst`.
    let tryProjectStepFromAst (transformerId: string) (pluginId: string) (block: BlockDeclarationSyntax) : DashflowTransformStepDef option =
        Some(projectStepFromAst transformerId pluginId block)
