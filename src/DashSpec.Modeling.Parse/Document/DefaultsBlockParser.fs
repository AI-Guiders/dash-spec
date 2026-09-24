namespace DashSpec.Modeling.Parse.Document

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

module DefaultsBlockParser =

    let private filterKeyPrefix = "filter."

    let tryParseFilterDefaultKey (key: string) =
        if not (key.StartsWith(filterKeyPrefix, StringComparison.OrdinalIgnoreCase)) then
            None
        elif key.EndsWith(".default", StringComparison.OrdinalIgnoreCase) then
            raise (DashSpecParseException("In defaults block use filter.<id> = … (no '.default' suffix)."))
        else
            let filterName = key.Substring(filterKeyPrefix.Length)

            if String.IsNullOrWhiteSpace filterName || filterName.Contains '.' then
                None
            else
                Some filterName

    let private readDefaultValue (reader: TokenReader) =
        match reader.RawKind with
        | TokenKind.RelativeDay -> reader.ReadDateDefaultValue()
        | _ -> reader.ReadScalarValue()

    let parse
        (reader: TokenReader)
        (blockKeyword: string)
        (formatDefaults: ReportFormatDefaults)
        (filterDefaults: Dictionary<string, string>)
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
                    match tryParseFilterDefaultKey key with
                    | Some filterName ->
                        reader.Expect TokenKind.Eq
                        filterDefaults.[filterName] <- readDefaultValue reader
                        reader.SkipNewlines()
                    | None ->
                        raise (DashSpecParseException($"Unknown defaults property '{key}'. Use time_format, date_format, datetime_format, or filter.<id>."))

        BlockSyntax.expectBlockEnd reader blockKeyword (None: string option)
        formats
