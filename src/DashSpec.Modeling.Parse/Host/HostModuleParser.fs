namespace DashSpec.Modeling.Parse.Host

open System
open System.Collections.Generic
open System.IO
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Layout
open DashSpec.Modeling.Parse.Lexing

module HostModuleParser =

    let private readIncludeReference (reader: TokenReader) =
        let kind = reader.ReadIdent()
        let reference = reader.ReadString()
        kind, reference

    let private assignTopbarLayout
        (specDirectory: string)
        (context: string)
        (topbarLayout: LayoutBoardDefinition option byref)
        (reference: string)
        =
        let board = LayoutModuleParser.load reference specDirectory
        LayoutModuleScopeValidator.ensureMatchesIncludeSite board LayoutScope.Host context
        topbarLayout <- Some board

    let private readBoolProperty (reader: TokenReader) (defaultValue: bool) =
        reader.Expect TokenKind.Eq
        let value = reader.ReadIdent()
        if String.IsNullOrWhiteSpace value then defaultValue
        else not (value.Equals("false", StringComparison.OrdinalIgnoreCase))

    let private parseLink (reader: TokenReader) =
        let linkId = reader.ReadIdent()
        if String.IsNullOrWhiteSpace linkId then
            raise (DashSpecParseException("Host link requires an id."))

        let mutable label = linkId
        if reader.TryKeyword "as" then
            label <- reader.ReadString()

        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let mutable url = ""
        let mutable target = "_blank"
        let mutable topbar = true
        let mutable settings = true
        while not (BlockSyntax.isBlockEnd reader "link" (Some linkId)) && not reader.IsEof do
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader "link" (Some linkId) then ()
            elif reader.TryKeyword "url" then
                reader.Expect TokenKind.Eq
                url <- reader.ReadString()
                reader.SkipNewlines()
            elif reader.TryKeyword "target" then
                reader.Expect TokenKind.Eq
                target <- reader.ReadString()
                reader.SkipNewlines()
            elif reader.TryKeyword "topbar" then
                topbar <- readBoolProperty reader true
                reader.SkipNewlines()
            elif reader.TryKeyword "settings" then
                settings <- readBoolProperty reader true
                reader.SkipNewlines()
            else
                raise (reader.Unexpected())
        BlockSyntax.expectBlockEnd reader "link" (Some linkId)
        reader.SkipNewlines()

        if String.IsNullOrWhiteSpace url then
            raise (DashSpecParseException($"Host link '{linkId}' requires url."))

        { Id = linkId
          Label = label
          Url = url
          Target = target
          Topbar = topbar
          Settings = settings }

    let private parseLinksBlock (reader: TokenReader) (links: ResizeArray<HostLinkDefinition>) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        while not (BlockSyntax.isBlockEnd reader "links" None) && not reader.IsEof do
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader "links" None then ()
            elif reader.TryKeyword "link" then
                links.Add(parseLink reader)
            else
                raise (reader.Unexpected())
        BlockSyntax.expectBlockEnd reader "links" None
        reader.SkipNewlines()

    let private parseSurfacesBlock (reader: TokenReader) (surfaces: ResizeArray<string>) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        while not (BlockSyntax.isBlockEnd reader "surfaces" None) && not reader.IsEof do
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader "surfaces" None then ()
            elif reader.TryKeyword "show" then
                let mutable continueItems = true
                while continueItems && not (reader.IsOnNewline()) && not reader.IsEof do
                    if reader.RawKind = TokenKind.Ident then
                        surfaces.Add(reader.ReadIdentSameLine())
                    if reader.IsAt TokenKind.Comma then
                        reader.Advance()
                    else
                        continueItems <- false
                reader.SkipNewlines()
            else
                raise (reader.Unexpected())
        BlockSyntax.expectBlockEnd reader "surfaces" None
        reader.SkipNewlines()

    let parse (text: string) (specDirectory: string option) : HostDocument =
        if String.IsNullOrWhiteSpace text then invalidArg "text" "Host text is required."

        let reader = ParserUtilities.createReader text
        reader.SkipFileDirectives()
        reader.SkipNewlines()
        reader.Expect TokenKind.At
        reader.ExpectKeyword "host"
        let hostId = reader.ReadIdent()
        if String.IsNullOrWhiteSpace hostId then
            raise (DashSpecParseException("@host module requires @host <id>."))

        BlockSyntax.beginBlock reader
        reader.SkipNewlines()

        let mutable catalogPath = ""
        let configuration = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        let presentation = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        let links = ResizeArray<HostLinkDefinition>()
        let surfaces = ResizeArray<string>()
        let mutable topbarLayout = None

        while not (BlockSyntax.isBlockEnd reader "host" (Some hostId)) && not reader.IsEof do
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader "host" (Some hostId) then ()
            elif reader.TryKeyword "catalog" then
                catalogPath <- reader.ReadString()
                reader.SkipNewlines()
            elif reader.TryKeyword "configuration" then
                let props =
                    PropertyBlockParser.parse
                        reader
                        PropertySchemas.hostConfiguration
                        "configuration"
                        false
                        false
                for kv in props do
                    configuration.[kv.Key] <- kv.Value
                reader.SkipNewlines()
            elif reader.TryKeyword "presentation" then
                let props =
                    PropertyBlockParser.parse
                        reader
                        PropertySchemas.hostPresentation
                        "presentation"
                        false
                        false
                for kv in props do
                    presentation.[kv.Key] <- kv.Value
                reader.SkipNewlines()
            elif reader.TryKeyword "links" then
                parseLinksBlock reader links
            elif reader.TryKeyword "surfaces" then
                parseSurfacesBlock reader surfaces
            else
                match reader.TryModuleInclude() with
                | Some reference ->
                    match specDirectory with
                    | None | Some "" ->
                        raise (DashSpecParseException("!include requires host file directory when parsing."))
                    | Some dir ->
                        assignTopbarLayout dir "!include" &topbarLayout reference
                        reader.SkipNewlines()
                | None ->
                    if reader.TryKeyword "include" then
                        let kind, reference = readIncludeReference reader
                        if not (String.Equals(kind, "layout", StringComparison.OrdinalIgnoreCase)) then
                            raise (DashSpecParseException($"@host module allows include layout only, got include {kind}."))
                        match specDirectory with
                        | None | Some "" ->
                            raise (DashSpecParseException("include layout requires host file directory when parsing."))
                        | Some dir ->
                            assignTopbarLayout dir "include layout" &topbarLayout reference
                            reader.SkipNewlines()
                    else
                        raise (reader.Unexpected())

        BlockSyntax.expectBlockEnd reader "host" (Some hostId)

        if String.IsNullOrWhiteSpace catalogPath then
            raise (DashSpecParseException($"@host '{hostId}' requires catalog \"path/to.dashcatalog\"."))

        { Id = hostId
          CatalogPath = catalogPath
          Configuration = configuration
          Presentation = presentation
          Links = links :> IReadOnlyList<_>
          Surfaces = surfaces :> IReadOnlyList<_>
          TopbarLayout = topbarLayout }

    let parseFile (path: string) : HostDocument =
        if String.IsNullOrWhiteSpace path then invalidArg "path" "Host file path is required."
        if not (File.Exists path) then
            raise (FileNotFoundException($"Host file not found: '{path}'.", path))
        let directory = Path.GetDirectoryName(Path.GetFullPath path)
        parse (File.ReadAllText path) (Some directory)
