namespace DashSpec.Modeling.Parse.Layout

open System
open System.IO
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

/// <summary>M2 pilot — F# lex + parse SSOT for <c>.dashlayout</c> (ADR-0048).</summary>
module LayoutModuleParser =

    let private parseMandatoryScope (reader: TokenReader) =
        if not (reader.TryKeyword "scope") then
            raise (DashSpec.Modeling.Core.DashSpecParseException("Layout module requires scope toolbar|tab|page|card after @layout <id>."))

        let kind = reader.ReadIdent()

        if String.IsNullOrWhiteSpace kind then
            raise (DashSpec.Modeling.Core.DashSpecParseException("Layout module requires scope toolbar|tab|page|card after @layout <id>."))

        match kind.ToLowerInvariant() with
        | "toolbar" -> LayoutScope.Toolbar
        | "tab" -> LayoutScope.Tab
        | "page" -> LayoutScope.Page
        | "card" -> LayoutScope.Card
        | _ ->
            raise (DashSpec.Modeling.Core.DashSpecParseException($"Layout module scope must be toolbar, tab, page, or card; got '{kind}'."))

    /// <summary>Parse a <c>.dashlayout</c> file body.</summary>
    let parseLayoutFile (text: string) : LayoutBoardDefinition =
        if String.IsNullOrWhiteSpace text then
            invalidArg "text" "Layout text is required."

        let reader = ParserUtilities.createReader text
        reader.SkipFileDirectives()
        reader.Expect TokenKind.At
        reader.ExpectKeyword "layout"

        let id = reader.ReadIdent()

        if String.IsNullOrWhiteSpace id then
            raise (DashSpec.Modeling.Core.DashSpecParseException("Layout module requires @layout <id>."))

        reader.SkipNewlines()
        let scope = parseMandatoryScope reader
        reader.SkipNewlines()
        let board = LayoutParser.parseBoardRows reader None None
        { board with ModuleScope = Some scope }

    let load (reference: string) (specDirectory: string) =
        if String.IsNullOrWhiteSpace reference then
            invalidArg "reference" "Layout reference is required."
        if String.IsNullOrWhiteSpace specDirectory then
            invalidArg "specDirectory" "Spec directory is required."

        let path = SpecIncludeResolver.resolvePath reference specDirectory |> SpecIncludeResolver.resolveLayoutFile

        if not (File.Exists path) then
            raise (FileNotFoundException($"Include layout not found: '{reference}' (resolved: {path}).", path))

        parseLayoutFile (File.ReadAllText path)
