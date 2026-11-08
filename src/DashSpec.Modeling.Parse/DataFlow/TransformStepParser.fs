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
