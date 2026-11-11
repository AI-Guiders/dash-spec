namespace DashSpec.Modeling.Parse.DataFlow

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Lexing

module TransformStepParser =

    /// Declared param kinds → lexer token kinds ("ident" covers column names,
    /// enum values, slash chains). "string" is universal: every declared kind also
    /// accepts a plain string (the old contracts did the same).
    let private declaredTokenKind =
        function
        | "timeshift" -> Some TokenKind.TimeShift
        | "iana" -> Some TokenKind.IanaZone
        | "string" -> Some TokenKind.String
        | "ident" -> Some TokenKind.Ident
        | _ -> None

    let private readAssignmentValue (reader: TokenReader) =
        reader.SkipNewlines()

        let first =
            match reader.CurrentKind with
            | TokenKind.String -> reader.ReadString()
            | TokenKind.TimeShift -> reader.ReadTimeShift()
            | TokenKind.IanaZone -> reader.ReadIanaZone()
            | TokenKind.Ident -> reader.ReadIdent()
            | _ -> raise (reader.Unexpected "assignment value")

        let sb = System.Text.StringBuilder(first)

        while not (reader.IsOnNewline()) && reader.RawKind = TokenKind.Slash do
            reader.Advance()
            sb.Append('/') |> ignore
            ignore (sb.Append(reader.ReadIdentSameLine()))

        sb.ToString()

    /// The stdlib declares each transform's parameters (stdlib/transforms/**.ds,
    /// registered lazily — TransformDeclarationParser.ensureStdlibRegistered).
    /// A declared param's kind gates the literal; anything else → the generic
    /// assignment reader. The parser knows no transform names.
    let private readParameterValue (pluginId: string) (key: string) (reader: TokenReader) =
        match TransformDeclarations.requirementFor pluginId key with
        | Some (kinds, hint) ->
            let accepts kind =
                kind = TokenKind.String
                || kinds |> List.exists (fun declared -> declaredTokenKind declared = Some kind)

            match reader.CurrentKind with
            | kind when accepts kind -> readAssignmentValue reader
            | _ -> raise (DashSpecParseException(hint))
        | None -> readAssignmentValue reader

    let parseTransformUseBlock (reader: TokenReader) (transformerId: string) : DashflowTransformStepDef =
        TransformDeclarationParser.ensureStdlibRegistered ()
        reader.ExpectKeyword "use" |> ignore
        let pluginId = reader.ReadIdent()

        // The vocabulary is the stdlib's declarations (ADR-0126): a typo or a dead
        // keyword fails here, at parse time — never at render.
        if not (TransformDeclarations.isKnown pluginId) then
            let known =
                TransformDeclarations.registered ()
                |> List.map (fun d -> d.Id)
                |> Array.ofList
                |> fun ids -> String.Join("|", ids)

            raise (
                DashSpecParseException(
                    $"transformer '{transformerId}': unknown transform '{pluginId}' — declared: {known}"))

        if reader.IsAt TokenKind.LBrace then
            reader.Expect TokenKind.LBrace
            let parameters = ResizeArray<DashflowTransformParam>()

            while not (reader.IsAt TokenKind.RBrace) && not reader.IsEof do
                reader.SkipNewlines()

                if reader.IsAt TokenKind.RBrace then ()
                else
                    let key = reader.ReadIdent()
                    reader.Expect TokenKind.Eq
                    let value = readParameterValue pluginId key reader
                    parameters.Add({ Key = key; Value = value })

            reader.Expect TokenKind.RBrace

            let raw =
                parameters.ToArray() |> Array.map (fun p -> p.Key, p.Value)

            let normalized =
                BuiltinScalarTransforms.normalizeStep transformerId pluginId raw
                |> Array.map (fun (k, v) -> { Key = k; Value = v })

            { PluginId = pluginId; Parameters = normalized }
        else
            match TransformDeclarations.tryFind pluginId with
            | Some decl when decl.RequiresParameterBlock ->
                raise (
                    DashSpecParseException(
                        $"transformer '{transformerId}': transform use {pluginId} requires a parameter block."))
            | _ -> { PluginId = pluginId; Parameters = [||] }

    let private unwrapStringLiteral (raw: string) =
        let trimmed = raw.Trim()
        if trimmed.Length >= 2 && trimmed.[0] = '"' && trimmed.[trimmed.Length - 1] = '"' then
            trimmed.Substring(1, trimmed.Length - 2)
        else
            trimmed

    let private validateAndExtractParamValue (pluginId: string) (prop: DashSpec.Modeling.Parse.Syntax.PropertySyntax) : string option =
        if prop.ValueTokens.Length = 0 then
            None
        else
            // Allowed value shapes: String, TimeShift, IanaZone, Ident, or Ident/Ident slash chain
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
    let tryProjectStepFromAst (transformerId: string) (pluginId: string) (block: DashSpec.Modeling.Parse.Syntax.BlockDeclarationSyntax) : DashflowTransformStepDef option =
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
            | _ -> Some { PluginId = pluginId; Parameters = [||] }
        else
            let parameters = ResizeArray<string * string>()
            let mutable ok = true

            for memberNode in block.Members do
                if ok then
                    match memberNode with
                    | DashSpec.Modeling.Parse.Syntax.DashSpecAstNode.PropertyAssignment prop ->
                        match validateAndExtractParamValue pluginId prop with
                        | Some value -> parameters.Add(prop.Key, value)
                        | None -> ok <- false
                    | DashSpec.Modeling.Parse.Syntax.DashSpecAstNode.BlankLine _
                    | DashSpec.Modeling.Parse.Syntax.DashSpecAstNode.EndBlock _ -> ()
                    | memberNode when DashSpec.Modeling.Parse.Syntax.DashSpecAst.isTrivia memberNode -> ()
                    | _ -> ok <- false

            if not ok then
                None
            else
                let normalized =
                    BuiltinScalarTransforms.normalizeStep transformerId pluginId (parameters.ToArray())
                    |> Array.map (fun (k, v) -> { Key = k; Value = v })

                Some { PluginId = pluginId; Parameters = normalized }
