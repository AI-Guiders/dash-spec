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

    type ParsedPorts =
        { Inputs: ParsedPortDecl[]
          Outputs: ParsedPortDecl[] }

    let toDashPortType (decl: ParsedPortDecl) =
        match decl.Shape with
        | PortShape.Stream -> DashPortType.Stream decl.ValueType
        | PortShape.Scalar -> DashPortType.Scalar decl.ValueType

    let private readStreamShapeKeyword (reader: TokenReader) =
        if reader.TryKeyword "table" then
            raise (DashSpecParseException("row-batch ports use 'stream', not 'table'."))

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

    let parsePortsBlock (reader: TokenReader) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let inputs = ResizeArray<ParsedPortDecl>()
        let outputs = ResizeArray<ParsedPortDecl>()

        while not (BlockSyntax.isBlockEnd reader "ports" None) && not reader.IsEof do
            reader.SkipNewlines()

            if BlockSyntax.isBlockEnd reader "ports" None then ()
            elif reader.TryKeyword "input" then
                let decl = readPortLine reader readInputShape
                inputs.Add decl
            elif reader.TryKeyword "output" then
                let decl = readPortLine reader readOutputShape
                outputs.Add decl
            else
                raise (reader.Unexpected "input or output in ports block")

            reader.SkipNewlines()

        BlockSyntax.expectBlockEnd reader "ports" None

        { Inputs = inputs.ToArray()
          Outputs = outputs.ToArray() }
