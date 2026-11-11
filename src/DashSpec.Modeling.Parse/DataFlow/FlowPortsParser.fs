namespace DashSpec.Modeling.Parse.DataFlow

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

/// <c>ports</c> … <c>end ports</c> — <c>input stream|scalar Name: Type</c>, <c>output stream Name: Type</c>.
module FlowPortsParser =

    [<RequireQualifiedAccess>]
    type PortShape =
        | Stream
        | Scalar

    type ParsedPortDecl =
        { Name: string
          ValueType: string
          Shape: PortShape }

    /// <summary><c>param name: type = default</c> — typed widget-value port (ADR-0120).</summary>
    type ParsedParamDecl =
        { Name: string
          ValueType: string
          Default: string option
          ColumnBinding: string option }

    type ParsedPorts =
        { Inputs: ParsedPortDecl[]
          Outputs: ParsedPortDecl[]
          Params: ParsedParamDecl[]
          DefaultInput: string option
          DefaultOutput: string option }

    let toDashPortType (decl: ParsedPortDecl) =
        match decl.Shape with
        | PortShape.Stream -> DashPortType.Stream decl.ValueType
        | PortShape.Scalar -> DashPortType.Scalar decl.ValueType

    let private readStreamShapeKeyword (reader: TokenReader) =
        if reader.TryKeyword "stream" then
            PortShape.Stream
        else
            PortShape.Stream

    let private readInputShape (reader: TokenReader) =
        if reader.TryKeyword "scalar" then
            PortShape.Scalar
        else
            readStreamShapeKeyword reader

    let private readOutputShape (reader: TokenReader) =
        if reader.TryKeyword "scalar" then
            raise (DashSpecParseException("output ports must be stream (row batch)."))
        else
            readStreamShapeKeyword reader

    let private readValueTypeName (reader: TokenReader) =
        reader.SkipNewlines()

        let typeName =
            if reader.TryKeyword "rows" then
                reader.ReadIdent()
            else
                reader.ReadIdent()

        if String.IsNullOrWhiteSpace typeName then
            raise (DashSpecParseException("port declaration requires a type name after ':'."))

        typeName

    let private readPortLine (reader: TokenReader) (shapeFor: TokenReader -> PortShape) =
        let shape = shapeFor reader
        let portName = reader.ReadIdent()

        if String.IsNullOrWhiteSpace portName then
            raise (DashSpecParseException("port name is required."))

        reader.Expect TokenKind.Colon
        let valueType = readValueTypeName reader
        { Name = portName; ValueType = valueType; Shape = shape }

    /// <summary><c>param name: type = default [bind column]</c>. The default accepts
    /// a string literal or a bare word (<c>all</c>, <c>today</c>, numbers) — stored
    /// raw, typed binding happens downstream. <c>bind &lt;column&gt;</c> (ADR-0125 §3)
    /// declares the pushed-down predicate column — the iTVF emission's input.</summary>
    let private readParamLine (reader: TokenReader) =
        let paramName = reader.ReadIdent()

        if String.IsNullOrWhiteSpace paramName then
            raise (DashSpecParseException("param declaration requires a name."))

        reader.Expect TokenKind.Colon
        let valueType = readValueTypeName reader

        let defaultValue =
            if reader.IsAt TokenKind.Eq then
                reader.Expect TokenKind.Eq |> ignore

                if reader.IsAt TokenKind.String then
                    Some(reader.ReadString())
                else
                    let word = reader.ReadIdent()

                    if String.IsNullOrWhiteSpace word then
                        raise (DashSpecParseException($"param '{paramName}': '=' requires a default value."))

                    Some word
            else
                None

        let columnBinding =
            if reader.TryKeyword "bind" then
                let column = reader.ReadIdent()

                if String.IsNullOrWhiteSpace column then
                    raise (DashSpecParseException($"param '{paramName}': bind requires a column name."))

                Some column
            else
                None

        { Name = paramName; ValueType = valueType; Default = defaultValue; ColumnBinding = columnBinding }

    let private validateDefaultPort (role: string) (name: string) (decls: ParsedPortDecl[]) =
        if String.IsNullOrWhiteSpace name then
            raise (DashSpecParseException($"default {role} requires a port name."))

        if not (decls |> Array.exists (fun d -> String.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase))) then
            raise (DashSpecParseException($"default {role} '{name}' is not declared in this ports block."))

    let parsePortsBlock (reader: TokenReader) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let inputs = ResizeArray<ParsedPortDecl>()
        let outputs = ResizeArray<ParsedPortDecl>()
        let paramsDecls = ResizeArray<ParsedParamDecl>()
        let mutable defaultInput: string option = None
        let mutable defaultOutput: string option = None

        while not (BlockSyntax.isBlockEnd reader "ports" None) && not reader.IsEof do
            reader.SkipNewlines()

            if BlockSyntax.isBlockEnd reader "ports" None then ()
            elif reader.TryKeyword "default" then
                if reader.TryKeyword "input" then
                    let name = reader.ReadIdent()
                    defaultInput <- Some name
                elif reader.TryKeyword "output" then
                    let name = reader.ReadIdent()
                    defaultOutput <- Some name
                else
                    raise (DashSpecParseException("default requires input or output and a port name."))
            elif reader.TryKeyword "input" then
                let decl = readPortLine reader readInputShape
                inputs.Add decl
            elif reader.TryKeyword "output" then
                let decl = readPortLine reader readOutputShape
                outputs.Add decl
            elif reader.TryKeyword "param" then
                paramsDecls.Add(readParamLine reader)
            else
                raise (reader.Unexpected "default, input, param, or output in ports block")

            reader.SkipNewlines()

        BlockSyntax.expectBlockEnd reader "ports" None

        let inputsArr = inputs.ToArray()
        let outputsArr = outputs.ToArray()

        defaultInput |> Option.iter (fun name -> validateDefaultPort "input" name inputsArr)
        defaultOutput |> Option.iter (fun name -> validateDefaultPort "output" name outputsArr)

        { Inputs = inputsArr
          Outputs = outputsArr
          Params = paramsDecls.ToArray()
          DefaultInput = defaultInput
          DefaultOutput = defaultOutput }

    /// <summary>ADR-0130: project <see cref="ParsedPorts"/> from a typed
    /// ports-block AST (<c>DashSpecAstNode</c> members). None when the block carries
    /// any unrecognized member — the caller falls back to the token path.</summary>
    let tryProjectPortsFromAst (members: DashSpec.Modeling.Parse.Syntax.DashSpecAstNode[]) : ParsedPorts option =
        let inputs = ResizeArray<ParsedPortDecl>()
        let outputs = ResizeArray<ParsedPortDecl>()
        let paramsDecls = ResizeArray<ParsedParamDecl>()
        let mutable defaultInput = None
        let mutable defaultOutput = None
        let mutable ok = true

        for memberNode in members do
            match memberNode with
            | DashSpec.Modeling.Parse.Syntax.DashSpecAstNode.PortDeclaration decl ->
                let shape =
                    match decl.PortShape with
                    | Some s when String.Equals(s, "scalar", StringComparison.OrdinalIgnoreCase) -> PortShape.Scalar
                    | _ -> PortShape.Stream

                match decl.Role with
                | DashSpec.Modeling.Parse.Syntax.PortDeclRole.PortInput ->
                    inputs.Add { Name = decl.PortName; ValueType = decl.ValueType |> Option.defaultValue ""; Shape = shape }
                | DashSpec.Modeling.Parse.Syntax.PortDeclRole.PortOutput ->
                    if shape = PortShape.Scalar then
                        raise (DashSpecParseException("output ports must be stream (row batch)."))
                    outputs.Add { Name = decl.PortName; ValueType = decl.ValueType |> Option.defaultValue ""; Shape = shape }
                | DashSpec.Modeling.Parse.Syntax.PortDeclRole.PortParam ->
                    paramsDecls.Add
                        { Name = decl.PortName
                          ValueType = decl.ValueType |> Option.defaultValue ""
                          Default = decl.DefaultValue
                          ColumnBinding = decl.ColumnBinding }
            | DashSpec.Modeling.Parse.Syntax.DashSpecAstNode.ClauseDeclaration clause ->
                match clause.Kind with
                | DashSpec.Modeling.Parse.Syntax.ClauseKind.DefaultPortClause(direction, portName) ->
                    if String.Equals(direction, "input", StringComparison.OrdinalIgnoreCase) then
                        defaultInput <- Some portName
                    else
                        defaultOutput <- Some portName
                | _ -> ok <- false
            | DashSpec.Modeling.Parse.Syntax.DashSpecAstNode.BlankLine _
            | DashSpec.Modeling.Parse.Syntax.DashSpecAstNode.EndBlock _ -> ()
            | memberNode when DashSpec.Modeling.Parse.Syntax.DashSpecAst.isTrivia memberNode -> ()
            | _ -> ok <- false

        if not ok then
            None
        else
            let inputsArr = inputs.ToArray()
            let outputsArr = outputs.ToArray()

            defaultInput |> Option.iter (fun name -> validateDefaultPort "input" name inputsArr)
            defaultOutput |> Option.iter (fun name -> validateDefaultPort "output" name outputsArr)

            Some
                { Inputs = inputsArr
                  Outputs = outputsArr
                  Params = paramsDecls.ToArray()
                  DefaultInput = defaultInput
                  DefaultOutput = defaultOutput }
