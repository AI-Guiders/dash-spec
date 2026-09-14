namespace DashSpec.Modeling.Parse.Document

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

module rec DashboardParser =

    let readRuntimePath (text: string) = DocumentModuleParser.readRuntimeManifest text

    [<Obsolete("Use ReadRuntimePath. @config is a deprecated alias for @runtime.")>]
    let readConfigPath (text: string) = readRuntimePath text

    let readDiagramLibraryPath (text: string) = DocumentModuleParser.readConfigurationValue text "diagramlibrary"

    let readPalettePath (text: string) = DocumentModuleParser.readConfigurationValue text "palette"

    let readSqlDialect (text: string) =
        match DocumentModuleParser.readConfigurationValue text "sqldialect" with
        | Some dialect -> SqlDialectParser.parse dialect
        | None -> SqlDialect.TSql

    let readDashboardHeader (text: string) =
        if not (DocumentModuleParser.isBlockModuleFormat text) then
            raise (DashSpecParseException("ReadDashboardHeader requires block module format."))
        else
            readBlockDashboardHeader text

    let readPaletteReference (reader: TokenReader) =
        if reader.RawKind = TokenKind.Eq then
            reader.Advance()
        reader.ReadScalarValue()

    let private skipEnvelopeSection (reader: TokenReader) =
        if reader.TryKeyword "runtime" || reader.TryKeyword "configuration" || reader.TryKeyword "wiring" then
            skipBlock reader
        elif reader.TryModuleInclude().IsSome then ()
        else
            raise (reader.Unexpected())

    let private skipBlock (reader: TokenReader) =
        reader.Expect TokenKind.LBrace
        let mutable depth = 1
        while depth > 0 && not reader.IsEof do
            if reader.IsAt TokenKind.LBrace then
                reader.Advance()
                depth <- depth + 1
            elif reader.IsAt TokenKind.RBrace then
                reader.Advance()
                depth <- depth - 1
            else
                reader.Advance()

    let private readBlockDashboardHeader (text: string) =
        let reader = ParserUtilities.createReader text
        reader.SkipNewlines()
        reader.Expect TokenKind.At

        if reader.TryKeyword "tab" then
            let id = reader.ReadIdent()
            id, id
        else
            reader.ExpectKeyword "dashboard"
            let dashboardId = reader.ReadIdent()
            reader.SkipNewlines()
            reader.Expect TokenKind.LBrace

            let mutable title = dashboardId
            while not (reader.IsAt TokenKind.RBrace) && not reader.IsEof do
                reader.SkipNewlines()
                if reader.TryKeyword "report" then
                    title <-
                        if reader.CurrentKind = TokenKind.String then reader.ReadString()
                        else dashboardId
                else
                    skipEnvelopeSection reader

            dashboardId, title
