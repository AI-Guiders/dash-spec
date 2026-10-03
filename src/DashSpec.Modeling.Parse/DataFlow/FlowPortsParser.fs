namespace DashSpec.Modeling.Parse.DataFlow

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

/// <c>ports</c> … <c>end ports</c> — rowset port declarations (<c>input Name: Type</c> / <c>output Name: Type</c>).
module FlowPortsParser =

    type ParsedPorts =
        { Inputs: (string * string)[]
          Outputs: (string * string)[] }

    let private readRowTypeName (reader: TokenReader) =
        reader.SkipNewlines()

        let typeName =
            if reader.TryKeyword "rows" then
                reader.ReadIdent()
            else
                reader.ReadIdent()

        if String.IsNullOrWhiteSpace typeName then
            raise (DashSpecParseException("port declaration requires a row type name after ':'."))

        typeName

    let private readPortLine (reader: TokenReader) =
        let portName = reader.ReadIdent()

        if String.IsNullOrWhiteSpace portName then
            raise (DashSpecParseException("port name is required."))

        reader.Expect TokenKind.Colon
        let rowType = readRowTypeName reader
        portName, rowType

    let parsePortsBlock (reader: TokenReader) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let inputs = ResizeArray<string * string>()
        let outputs = ResizeArray<string * string>()

        while not (BlockSyntax.isBlockEnd reader "ports" None) && not reader.IsEof do
            reader.SkipNewlines()

            if BlockSyntax.isBlockEnd reader "ports" None then ()
            elif reader.TryKeyword "input" then
                let decl = readPortLine reader
                inputs.Add decl
            elif reader.TryKeyword "output" then
                let decl = readPortLine reader
                outputs.Add decl
            else
                raise (reader.Unexpected "input or output in ports block")

            reader.SkipNewlines()

        BlockSyntax.expectBlockEnd reader "ports" None

        { Inputs = inputs.ToArray()
          Outputs = outputs.ToArray() }
