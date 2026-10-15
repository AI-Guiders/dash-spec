namespace DashSpec.Modeling.Parse.Host

open System
open System.Collections.Generic
open System.IO
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Layout
open DashSpec.Modeling.Parse.Lexing

module HostModuleParser =

    let private importTopbarLayout (specDirectory: string option) (sourceOffset: int) (namespaceName: string) =
        match specDirectory with
        | None
        | Some "" ->
            raise (DashSpecParseException("import layouts requires host file directory when parsing.", sourceOffset))
        | Some dir ->
            let index = DashSpec.Modeling.Parse.Document.UnitIndex.build dir

            let entries =
                index.Find namespaceName
                |> List.filter (fun entry ->
                    match entry.Kind with
                    | None -> true
                    | Some kind -> String.Equals(kind, "layout", StringComparison.OrdinalIgnoreCase)
                )

            match entries with
            | [ entry ] ->
                let board =
                    LayoutModuleParser.parseLayoutFile (
                        DashSpec.Modeling.Parse.Document.UnitSource.readUnit entry.Path
                    )

                LayoutModuleScopeValidator.ensureMatchesImportSite board LayoutScope.Host "import layouts"
                board
            | [] ->
                raise (DashSpecParseException($"No module found for import layouts from '{namespaceName}'.", sourceOffset))
            | _ ->
                raise (DashSpecParseException($"Host imports more than one layouts unit from '{namespaceName}'.", sourceOffset))

    let private parseLink (reader: TokenReader) =
        let linkId = reader.ReadIdent()
        if String.IsNullOrWhiteSpace linkId then
            raise (DashSpecParseException("Host link requires an id."))

        let mutable label = linkId
        if reader.TryKeyword "as" then
            label <- reader.ReadString()

        let props =
            BlockGrammar.parseKeywordContainer
                reader
                "link"
                $"host link '{linkId}'"
                [ BlockGrammar.SchemaProperties(PropertySchemas.hostLink, false, false) ]
                (Some linkId)

        reader.SkipNewlines()

        let url =
            match props.TryGetValue "url" with
            | true, value -> value
            | false, _ -> ""

        if String.IsNullOrWhiteSpace url then
            raise (DashSpecParseException($"Host link '{linkId}' requires url."))

        let readBool (key: string) (defaultValue: bool) =
            match props.TryGetValue key with
            | true, value when String.IsNullOrWhiteSpace value -> defaultValue
            | true, value -> not (value.Equals("false", StringComparison.OrdinalIgnoreCase))
            | false, _ -> defaultValue

        { Id = linkId
          Label = label
          Url = url
          Target =
            match props.TryGetValue "target" with
            | true, value when not (String.IsNullOrWhiteSpace value) -> value
            | _ -> "_blank"
          Topbar = readBool "topbar" true
          Settings = readBool "settings" true }

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
        reader.ExpectKeyword "module"
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
            elif reader.TryKeyword "import" then
                let sourceOffset = reader.CurrentOffset
                let kind = reader.ReadIdent()
                reader.ExpectKeyword "from"
                let namespaceName = AccessorGrammar.readQualifiedName reader

                if not (String.Equals(kind, "layouts", StringComparison.OrdinalIgnoreCase)) then
                    raise (DashSpecParseException($"@host module allows import layouts only, got import {kind}.", sourceOffset))

                topbarLayout <- Some(importTopbarLayout specDirectory sourceOffset namespaceName)
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
