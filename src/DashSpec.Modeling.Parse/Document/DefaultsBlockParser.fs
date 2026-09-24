namespace DashSpec.Modeling.Parse.Document

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

module DefaultsBlockParser =

    let private filterKeyPrefix = "filter."

    let tryParseFilterPropertyKey (key: string) =
        if not (key.StartsWith(filterKeyPrefix, StringComparison.OrdinalIgnoreCase)) then
            None
        elif key.EndsWith(".default", StringComparison.OrdinalIgnoreCase) then
            raise (DashSpecParseException("In defaults block use filter.<id>.<property> (not '.default')."))
        else
            let rest = key.Substring(filterKeyPrefix.Length)
            let parts = rest.Split('.', StringSplitOptions.RemoveEmptyEntries)

            match parts with
            | [| filterName; property |] when
                not (String.IsNullOrWhiteSpace filterName) && not (String.IsNullOrWhiteSpace property)
                ->
                Some(filterName, property)
            | [| filterName |] ->
                raise (DashSpecParseException($"Filter defaults require filter.<id>.<property>, got '{key}'."))
            | _ ->
                raise (DashSpecParseException($"Invalid filter defaults key '{key}'."))

    let private readDefaultValue (reader: TokenReader) =
        match reader.RawKind with
        | TokenKind.RelativeDay -> reader.ReadDateDefaultValue()
        | _ -> reader.ReadScalarValue()

    let parse
        (reader: TokenReader)
        (blockKeyword: string)
        (formatDefaults: ReportFormatDefaults)
        (filterDefaults: FilterScopeDefaults)
        =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let mutable formats = formatDefaults

        while not (BlockSyntax.isBlockEnd reader blockKeyword None) && not reader.IsEof do
            reader.SkipNewlines()

            if BlockSyntax.isBlockEnd reader blockKeyword None then ()
            else
                let key = reader.ReadIdent()

                if key.Equals("time_format", StringComparison.OrdinalIgnoreCase) then
                    reader.Expect TokenKind.Eq
                    formats <- { formats with TimeFormat = Some(reader.ReadString()) }
                    reader.SkipNewlines()
                elif key.Equals("date_format", StringComparison.OrdinalIgnoreCase) then
                    reader.Expect TokenKind.Eq
                    formats <- { formats with DateFormat = Some(reader.ReadString()) }
                    reader.SkipNewlines()
                elif key.Equals("datetime_format", StringComparison.OrdinalIgnoreCase) then
                    reader.Expect TokenKind.Eq
                    formats <- { formats with DateTimeFormat = Some(reader.ReadString()) }
                    reader.SkipNewlines()
                else
                    match tryParseFilterPropertyKey key with
                    | Some(filterName, property) ->
                        reader.Expect TokenKind.Eq
                        FilterScopeDefaults.set filterDefaults filterName property (readDefaultValue reader)
                        reader.SkipNewlines()
                    | None ->
                        raise (DashSpecParseException($"Unknown defaults property '{key}'. Use time_format, date_format, datetime_format, or filter.<id>.<property>."))

        BlockSyntax.expectBlockEnd reader blockKeyword (None: string option)
        formats
