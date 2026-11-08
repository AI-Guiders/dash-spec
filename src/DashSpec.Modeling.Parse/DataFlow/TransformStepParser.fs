namespace DashSpec.Modeling.Parse.DataFlow

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Lexing

module TransformStepParser =

    let private literalTokenKind =
        function
        | BuiltinScalarTransforms.ParamLiteral.TimeShiftZone -> TokenKind.TimeShift
        | BuiltinScalarTransforms.ParamLiteral.IanaZoneName -> TokenKind.IanaZone
        | BuiltinScalarTransforms.ParamLiteral.AnyString -> TokenKind.String

    let private readLiteralValue (reader: TokenReader) =
        reader.SkipNewlines()

        match reader.CurrentKind with
        | TokenKind.String -> reader.ReadString()
        | TokenKind.TimeShift -> reader.ReadTimeShift()
        | TokenKind.IanaZone -> reader.ReadIanaZone()
        | _ -> raise (reader.Unexpected "assignment value")

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

    /// Generic: each plugin declares its parameter contract next to its implementation
    /// (BuiltinScalarTransforms.paramContract, neutral literal kinds). The parser maps
    /// the contract to token kinds; no contract → free-form assignment reader. The
    /// parser knows no plugin names.
    let private readParameterValue (pluginId: string) (key: string) (reader: TokenReader) =
        let spec =
            BuiltinScalarTransforms.paramContract pluginId
            |> Option.bind (fun contract ->
                contract.Params
                |> List.tryFind (fun p -> String.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase)))

        match spec with
        | Some spec ->
            match reader.CurrentKind with
            | kind when spec.Literals |> List.exists (fun literal -> literalTokenKind literal = kind) ->
                readLiteralValue reader
            | _ -> raise (DashSpecParseException(spec.Hint))
        | None -> readAssignmentValue reader

    let parseTransformUseBlock (reader: TokenReader) (transformerId: string) : DashflowTransformStepDef =
        reader.ExpectKeyword "use"
        let pluginId = reader.ReadIdent()

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
            match BuiltinScalarTransforms.paramContract pluginId with
            | Some contract when contract.RequiresParameterBlock ->
                raise (
                    DashSpecParseException(
                        $"transformer '{transformerId}': transform use {pluginId} requires a parameter block."))
            | _ -> { PluginId = pluginId; Parameters = [||] }
