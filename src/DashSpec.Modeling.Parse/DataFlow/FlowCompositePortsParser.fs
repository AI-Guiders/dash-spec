namespace DashSpec.Modeling.Parse.DataFlow

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

/// <c>ports</c> on nested <c>flow</c> — parent-facing names; entry/exit inner nodes resolved later (ADR-0088).
module FlowCompositePortsParser =

    type CompositePortDecl =
        { ExternalName: string
          InnerPortName: string option }

    let parseCompositePortsBlock
        (reader: TokenReader)
        (inputs: ResizeArray<CompositePortDecl>)
        (outputs: ResizeArray<CompositePortDecl>)
        =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()

        while not (BlockSyntax.isBlockEnd reader "ports" None) && not reader.IsEof do
            reader.SkipNewlines()

            if BlockSyntax.isBlockEnd reader "ports" None then ()
            elif reader.TryKeyword "input" then
                let externalName = reader.ReadIdent()

                if String.IsNullOrWhiteSpace externalName then
                    raise (DashSpecParseException("nested flow ports: input requires a parent-facing port name."))

                let innerPort = FlowLinkParser.tryReadBracketPortSameLine reader

                inputs.Add { ExternalName = externalName; InnerPortName = innerPort }
            elif reader.TryKeyword "output" then
                let externalName = reader.ReadIdent()

                if String.IsNullOrWhiteSpace externalName then
                    raise (DashSpecParseException("nested flow ports: output requires a parent-facing port name."))

                let innerPort = FlowLinkParser.tryReadBracketPortSameLine reader

                outputs.Add { ExternalName = externalName; InnerPortName = innerPort }
            else
                raise (reader.Unexpected "input or output in nested flow ports block")

            reader.SkipNewlines()

        BlockSyntax.expectBlockEnd reader "ports" None
