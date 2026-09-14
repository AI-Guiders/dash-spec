namespace DashSpec.Modeling.Parse.Diagram

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

module InspectPresentationParser =

    let parse (reader: TokenReader) (context: string) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()

        let mutable tooltipId: string option = None
        let mutable label: string option = None
        let mutable format: string option = None
        let mutable split: string option = None

        while not (BlockSyntax.isBlockEnd reader "inspect" None) && not reader.IsEof do
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader "inspect" None then ()
            elif reader.TryKeyword "use" then
                if not (reader.TryKeyword "tooltip") then
                    raise (DashSpecParseException($"{context}: inspect 'use' requires 'tooltip <id>'."))
                let id = reader.ReadIdent()
                if String.IsNullOrWhiteSpace id then
                    raise (DashSpecParseException($"{context}: inspect use tooltip requires an id."))
                tooltipId <- Some id
            elif reader.TryKeyword "label" then
                reader.Expect TokenKind.Eq
                label <- Some(reader.ReadString())
            elif reader.TryKeyword "as" then
                let token = reader.ReadIdent()
                format <-
                    match token.ToLowerInvariant() with
                    | "list" | "bullets" | "ul" -> Some "list"
                    | "inline" | "line" | "text" -> Some "inline"
                    | _ ->
                        raise (DashSpecParseException($"{context}: inspect as must be list or inline; got '{token}'."))
            elif reader.TryKeyword "split" then
                reader.Expect TokenKind.Eq
                split <- Some(reader.ReadString())
            else
                raise (reader.Unexpected())

        BlockSyntax.expectBlockEnd reader "inspect" None

        if tooltipId.IsNone || String.IsNullOrWhiteSpace tooltipId.Value then
            raise (DashSpecParseException($"{context}: inspect requires 'use tooltip <id>'."))

        let resolvedFormat =
            match format with
            | Some f -> f
            | None when split.IsSome -> "list"
            | None -> "inline"

        let resolvedSplit =
            match split with
            | Some s when not (String.IsNullOrWhiteSpace s) -> s
            | _ -> ", "

        { TooltipId = tooltipId
          Label = label
          Format = resolvedFormat
          Split = resolvedSplit }
