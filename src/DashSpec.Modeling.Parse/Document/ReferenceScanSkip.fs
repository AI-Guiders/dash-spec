namespace DashSpec.Modeling.Parse.Document

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Diagram
open DashSpec.Modeling.Parse.Filter
open DashSpec.Modeling.Parse.Lexing
open DashSpec.Modeling.Parse.Toolbar
open DashSpec.Modeling.Parse.Card
open DashSpec.Modeling.Parse.DataFlow

/// Token-accurate skip/collect for module reference scanning (no regex).
module internal ReferenceScanSkip =

    let private consumeLine (reader: TokenReader) =
        while not (reader.IsOnNewline()) && not reader.IsEof do
            reader.Advance()

        reader.SkipNewlines()

    let private skipBalancedBlock (reader: TokenReader) (endKind: string) (endId: string option) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()

        while not (BlockSyntax.isBlockEnd reader endKind endId) && not reader.IsEof do
            reader.SkipNewlines()

            if BlockSyntax.isBlockEnd reader endKind endId then ()
            else
                while not (reader.IsOnNewline())
                      && not (BlockSyntax.isBlockEnd reader endKind endId)
                      && not reader.IsEof do
                    reader.Advance()

                reader.SkipNewlines()

        BlockSyntax.expectBlockEnd reader endKind endId

    let private skipBindBlock (reader: TokenReader) =
        if reader.IsOnNewline() then
            skipBalancedBlock reader "bind" None
        else
            reader.ReadCommaListInline() |> ignore
            reader.SkipNewlines()

    let private skipInputLine (reader: TokenReader) =
        CardFlowInputParser.parse reader "reference-scan" |> ignore
        reader.SkipNewlines()

    let private tryCollectDiagramPreset (reader: TokenReader) (diagramIds: HashSet<string>) =
        if not (reader.TryKeyword "diagram") then false
        else
            ParserUtilities.tryReadLayoutRef reader |> ignore

            match reader.TryPeekIdent() with
            | None -> consumeLine reader
            | Some name ->
                reader.ReadIdent() |> ignore
                reader.SkipNewlines()

                if reader.IsAt TokenKind.LBrace then
                    consumeLine reader
                elif DiagramKindRegistry.tryResolve name |> fst then
                    consumeLine reader
                else
                    diagramIds.Add name |> ignore
                    consumeLine reader

            true

    let private tryCollectRowsType (reader: TokenReader) (rowTypes: HashSet<string>) =
        if not (reader.TryKeyword "rows") then false
        else
            rowTypes.Add(reader.ReadIdent()) |> ignore
            consumeLine reader
            true

    let private skipFiltersToolbarTail (reader: TokenReader) =
        if reader.TryKeywordSameLine "dashboard" then
            ToolbarPlacementParser.discard reader "filters dashboard"
        elif reader.TryKeywordSameLine "chrome" then
            FiltersChromeParser.parse reader |> ignore
        else
            ToolbarPlacementParser.discard reader "toolbar"

        reader.SkipNewlines()

    let private skipEnvelopeSection (reader: TokenReader) (endKind: string) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()

        while not (BlockSyntax.isBlockEnd reader endKind None) && not reader.IsEof do
            reader.SkipNewlines()

            if BlockSyntax.isBlockEnd reader endKind None then ()
            else
                consumeLine reader

        BlockSyntax.expectBlockEnd reader endKind None

    let rec scanReportStatements (reader: TokenReader) (diagramIds: HashSet<string>) (rowTypes: HashSet<string>) =
        reader.SkipNewlines()

        while not (BlockSyntax.isBlockEnd reader "report" None) && not reader.IsEof do
            reader.SkipNewlines()

            if BlockSyntax.isBlockEnd reader "report" None then ()
            elif tryCollectDiagramPreset reader diagramIds then ()
            elif tryCollectRowsType reader rowTypes then ()
            elif reader.TryKeyword "title" then
                reader.Expect TokenKind.Eq
                reader.ReadString() |> ignore
                reader.SkipNewlines()
            elif reader.TryKeyword "type" then
                skipBalancedBlock reader "type" None
            elif reader.TryKeyword "standalone" then
                skipStandalone reader
            elif reader.TryKeyword "page" then
                let pageId = reader.ReadIdent()
                skipPageBlock reader pageId diagramIds rowTypes
            elif reader.TryKeyword "phase" then
                skipPhase reader diagramIds rowTypes
            elif reader.TryKeyword "card" then
                skipCardBlock reader diagramIds rowTypes
            elif reader.TryKeyword "commands" then
                CommandAliasesParser.parse reader |> ignore
                reader.SkipNewlines()
            elif reader.TryKeyword "defaults" then
                let scope = FilterScopeDefaults.create ()
                DefaultsBlockParser.parse reader "defaults" ReportFormatDefaults.empty scope |> ignore
                reader.SkipNewlines()
            elif reader.TryKeyword "default" then
                let scope = FilterScopeDefaults.create ()
                DefaultsBlockParser.parse reader "default" ReportFormatDefaults.empty scope |> ignore
                reader.SkipNewlines()
            elif reader.TryKeyword "cards" then
                if reader.TryKeyword "chrome" then
                    CardsChromeParser.parse reader |> ignore

                reader.SkipNewlines()
            elif reader.TryKeyword "filters" then
                match reader.TryPeekIdent() with
                | Some next when
                    String.Equals(next, "dashboard", StringComparison.OrdinalIgnoreCase)
                    || String.Equals(next, "chrome", StringComparison.OrdinalIgnoreCase)
                    ->
                    skipFiltersToolbarTail reader
                | _ -> skipBalancedBlock reader "filters" None
            elif reader.TryKeyword "toolbar" then
                skipFiltersToolbarTail reader
            elif reader.TryKeyword "tab" then
                TabParser.parse reader |> ignore
                reader.SkipNewlines()
            else
                consumeLine reader

    and skipPageBlock (reader: TokenReader) (pageId: string) (diagramIds: HashSet<string>) (rowTypes: HashSet<string>) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()

        while not (BlockSyntax.isBlockEnd reader "page" (Some pageId)) && not reader.IsEof do
            reader.SkipNewlines()

            if BlockSyntax.isBlockEnd reader "page" (Some pageId) then ()
            elif reader.TryKeyword "card" then
                skipCardBlock reader diagramIds rowTypes
            elif reader.TryKeyword "derive" then
                consumeLine reader
            elif reader.TryKeyword "bind" then
                skipBindBlock reader
            elif reader.TryKeyword "toolbar" || reader.TryKeyword "filters" then
                skipFiltersToolbarTail reader
            elif reader.TryKeyword "defaults" then
                skipBalancedBlock reader "defaults" None
            elif reader.TryKeyword "default" then
                skipBalancedBlock reader "default" None
            elif reader.TryKeyword "include" then
                consumeLine reader
            elif reader.TryModuleInclude().IsSome then
                reader.SkipNewlines()
            elif reader.TryKeyword "phase" then
                skipPhase reader diagramIds rowTypes
            else
                consumeLine reader

        BlockSyntax.expectBlockEnd reader "page" (Some pageId)

    and skipLayoutBlock (reader: TokenReader) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()

        while not (BlockSyntax.isBlockEnd reader "layout" None) && not reader.IsEof do
            reader.SkipNewlines()

            if BlockSyntax.isBlockEnd reader "layout" None then ()
            elif reader.TryKeyword "place" then
                skipBalancedBlock reader "place" None
            else
                consumeLine reader

        BlockSyntax.expectBlockEnd reader "layout" None

    and skipCardBlock (reader: TokenReader) (diagramIds: HashSet<string>) (rowTypes: HashSet<string>) =
        let cardId = reader.ReadIdent()
        ParserUtilities.tryReadLayoutRef reader |> ignore
        reader.SkipNewlines()

        if reader.CurrentKind = TokenKind.String then
            reader.ReadString() |> ignore

        reader.SkipNewlines()

        if reader.TryKeyword "title" then
            reader.Expect TokenKind.Eq
            reader.ReadString() |> ignore
            reader.SkipNewlines()

        BlockSyntax.beginBlock reader
        reader.SkipNewlines()

        while not (BlockSyntax.isBlockEnd reader "card" (Some cardId)) && not reader.IsEof do
            reader.SkipNewlines()

            if BlockSyntax.isBlockEnd reader "card" (Some cardId) then ()
            elif tryCollectDiagramPreset reader diagramIds then ()
            elif tryCollectRowsType reader rowTypes then ()
            elif reader.TryKeyword "view" then
                skipViewBlock reader diagramIds
            elif QualifiedFlowBlockParser.tryPeekQualifiedStart reader |> Option.isSome then
                let kind = QualifiedFlowBlockParser.consumeQualifiedStart reader
                let endKind = FlowGraphKindRegistry.keyword kind
                let endId = FlowGraphKindRegistry.blockEndId kind
                skipBalancedBlock reader endKind (Some endId)
            elif reader.TryKeyword "flow" then
                skipBalancedBlock reader "flow" None
            elif reader.TryKeyword "layout" then
                skipLayoutBlock reader
            elif reader.TryKeyword "filters" then
                if reader.TryKeywordSameLine "host" then
                    reader.ReadIdent() |> ignore
                    skipBalancedBlock reader "filters" None
                elif reader.IsOnNewline()
                     || (reader.TryPeekIdent().IsSome
                         && String.Equals(reader.TryPeekIdent().Value, "apply", StringComparison.OrdinalIgnoreCase)) then
                    skipBalancedBlock reader "filters" None
                else
                    consumeLine reader
            elif reader.TryKeyword "data" then
                if reader.TryKeyword "for" then
                    reader.ReadIdent() |> ignore

                skipDataBlock reader diagramIds rowTypes
            elif reader.TryKeyword "input" then
                skipInputLine reader
            elif reader.TryKeyword "bind" then
                skipBindBlock reader
            elif reader.TryKeyword "on" then
                reader.ReadIdent() |> ignore
                skipBalancedBlock reader "click" None
            elif reader.TryKeyword "override" then
                skipBalancedBlock reader "override" None
            elif reader.TryKeyword "overrides" then
                skipBalancedBlock reader "overrides" None
            elif reader.TryKeyword "when" then
                if reader.IsOnNewline() then
                    let whenTarget = reader.ReadIdent()
                    skipBalancedBlock reader "when" (Some whenTarget)
                else
                    consumeLine reader
            elif reader.TryKeyword "chrome" then
                skipBalancedBlock reader "chrome" None
            elif reader.TryKeyword "limits" then
                skipBalancedBlock reader "limits" None
            elif reader.TryKeyword "views" then
                skipBalancedBlock reader "views" None
            elif reader.TryKeyword "diagram" then
                skipDiagramLine reader diagramIds
            else
                consumeLine reader

        BlockSyntax.expectBlockEnd reader "card" (Some cardId)

    and skipDiagramLine (reader: TokenReader) (diagramIds: HashSet<string>) =
        if reader.TryKeyword "ref" then
            reader.ReadIdent() |> ignore

        match reader.TryPeekIdent() with
        | Some name ->
            reader.ReadIdent() |> ignore
            if not (DiagramKindRegistry.tryResolve name |> fst) then
                diagramIds.Add name |> ignore
        | None -> ()

        consumeLine reader

    and skipDataBlock (reader: TokenReader) (diagramIds: HashSet<string>) (rowTypes: HashSet<string>) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()

        while not (BlockSyntax.isBlockEnd reader "data" None) && not reader.IsEof do
            reader.SkipNewlines()

            if BlockSyntax.isBlockEnd reader "data" None then ()
            elif tryCollectRowsType reader rowTypes then ()
            elif tryCollectDiagramPreset reader diagramIds then ()
            elif reader.TryKeyword "datasource" then
                skipBalancedBlock reader "datasource" None
            elif reader.TryKeyword "bind" then
                skipBindBlock reader
            else
                consumeLine reader

        BlockSyntax.expectBlockEnd reader "data" None

    and skipViewBlock (reader: TokenReader) (diagramIds: HashSet<string>) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()

        while not (BlockSyntax.isBlockEnd reader "view" None) && not reader.IsEof do
            reader.SkipNewlines()

            if BlockSyntax.isBlockEnd reader "view" None then ()
            elif reader.TryKeyword "diagram" then
                skipDiagramLine reader diagramIds
            elif reader.TryKeyword "legend" then
                skipBalancedBlock reader "legend" None
            elif reader.TryKeyword "presentation" then
                skipBalancedBlock reader "presentation" None
            elif reader.TryKeyword "series" then
                skipBalancedBlock reader "series" None
            else
                consumeLine reader

        BlockSyntax.expectBlockEnd reader "view" None

    and skipStandalone (reader: TokenReader) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()

        while not (BlockSyntax.isBlockEnd reader "standalone" None) && not reader.IsEof do
            reader.SkipNewlines()

            if BlockSyntax.isBlockEnd reader "standalone" None then ()
            elif reader.TryKeyword "filter" then
                FilterParser.skipDeclaration reader
                reader.SkipNewlines()
            elif reader.TryKeyword "toolbar" || reader.TryKeyword "filters" then
                skipFiltersToolbarTail reader
            elif reader.TryKeyword "defaults" then
                skipBalancedBlock reader "defaults" None
            elif reader.TryKeyword "default" then
                skipBalancedBlock reader "default" None
            else
                consumeLine reader

        BlockSyntax.expectBlockEnd reader "standalone" None

    and skipPhase (reader: TokenReader) (diagramIds: HashSet<string>) (rowTypes: HashSet<string>) =
        let phaseId = reader.ReadIdent()
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()

        while not (BlockSyntax.isBlockEnd reader "phase" (Some phaseId)) && not reader.IsEof do
            reader.SkipNewlines()

            if BlockSyntax.isBlockEnd reader "phase" (Some phaseId) then ()
            elif reader.TryKeyword "card" then
                skipCardBlock reader diagramIds rowTypes
            elif reader.TryKeyword "page" then
                let pageId = reader.ReadIdent()
                skipPageBlock reader pageId diagramIds rowTypes
            else
                consumeLine reader

        BlockSyntax.expectBlockEnd reader "phase" (Some phaseId)

    let scanModuleText (text: string) =
        if String.IsNullOrWhiteSpace text then
            let empty = HashSet<string>(StringComparer.OrdinalIgnoreCase) :> ISet<_>
            empty, empty
        else
            let reader = ParserUtilities.createReader text
            let diagramIds = HashSet<string>(StringComparer.OrdinalIgnoreCase)
            let rowTypes = HashSet<string>(StringComparer.OrdinalIgnoreCase)

            reader.SkipNewlines()
            reader.Expect TokenKind.At
            let isDashboard = reader.TryKeyword "dashboard"

            if not isDashboard then
                reader.ExpectKeyword "tab"

            let moduleId = reader.ReadIdent()
            let moduleKind = if isDashboard then "dashboard" else "tab"

            BlockSyntax.beginBlock reader
            reader.SkipNewlines()
            ModuleHeaderParser.parse reader |> ignore
            reader.SkipNewlines()

            while not (BlockSyntax.isBlockEnd reader moduleKind (Some moduleId)) && not reader.IsEof do
                reader.SkipNewlines()

                if BlockSyntax.isBlockEnd reader moduleKind (Some moduleId) then ()
                elif reader.TryKeyword "report" then
                    if reader.CurrentKind = TokenKind.String then
                        reader.ReadString() |> ignore

                    reader.SkipNewlines()
                    BlockSyntax.beginBlock reader
                    reader.SkipNewlines()
                    scanReportStatements reader diagramIds rowTypes
                    BlockSyntax.expectBlockEnd reader "report" None
                elif reader.TryKeyword "runtime" then
                    skipEnvelopeSection reader "runtime"
                elif reader.TryKeyword "configuration" then
                    skipEnvelopeSection reader "configuration"
                elif reader.TryKeyword "connect" then
                    skipEnvelopeSection reader "connect"
                elif reader.TryKeyword "extensions" then
                    skipEnvelopeSection reader "extensions"
                elif reader.TryEnvelopeLinkDirective().IsSome then
                    reader.SkipNewlines()
                else
                    consumeLine reader

            diagramIds :> ISet<_>, rowTypes :> ISet<_>

    let scanReportAtReader (reader: TokenReader) =
        let scout = reader.Fork()
        BlockSyntax.beginBlock scout
        scout.SkipNewlines()
        let diagramIds = HashSet<string>(StringComparer.OrdinalIgnoreCase)
        let rowTypes = HashSet<string>(StringComparer.OrdinalIgnoreCase)
        scanReportStatements scout diagramIds rowTypes
        BlockSyntax.expectBlockEnd scout "report" None
        diagramIds :> ISet<_>, rowTypes :> ISet<_>
