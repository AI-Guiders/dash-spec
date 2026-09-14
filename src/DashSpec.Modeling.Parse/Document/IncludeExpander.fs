namespace DashSpec.Modeling.Parse.Document

open System
open System.IO
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Diagram
open DashSpec.Modeling.Parse.Include
open DashSpec.Modeling.Parse.Layout
open DashSpec.Modeling.Parse.Lexing
open DashSpec.Modeling.Parse.Presentation
open DashSpec.Modeling.Parse.Tooltip

module IncludeExpander =

    let private isIncomplete (reference: string) =
        if String.IsNullOrWhiteSpace reference then true
        elif reference.Contains '*' then false
        else reference.EndsWith "/" || reference.EndsWith "\\"

    let private resolveExistingIncludePath (reference: string) (specDirectory: string) =
        let path = SpecIncludeResolver.resolvePath reference specDirectory

        if File.Exists path then path
        else
            let extensions =
                [| ".dashlayout"; ".dashdiagram"; ".dashinclude"; ".dashpresentation"; ".dashtooltip" |]

            let mutable resolved = path

            for ext in extensions do
                let withExt =
                    if path.EndsWith(ext, StringComparison.OrdinalIgnoreCase) then path
                    else path + ext

                if File.Exists withExt then resolved <- withExt

            resolved

    let private resolvePaths (reference: string) (specDirectory: string) =
        if not (reference.Contains '*') then
            seq { yield resolveExistingIncludePath reference specDirectory }
        else
            let combined = Path.GetFullPath(Path.Combine(specDirectory, reference))
            let directory = Path.GetDirectoryName combined

            if String.IsNullOrWhiteSpace directory then
                raise (DashSpecParseException($"!include glob has no directory: '{reference}'."))

            let pattern = Path.GetFileName combined

            if String.IsNullOrWhiteSpace pattern then
                raise (DashSpecParseException($"!include glob requires a file pattern: '{reference}'."))

            if not (Directory.Exists directory) then
                Seq.empty
            else
                Directory.GetFiles(directory, pattern)
                |> Array.sortWith (fun left right -> String.Compare(left, right, StringComparison.OrdinalIgnoreCase))
                |> Seq.ofArray

    let private resolveLayoutPath (path: string) =
        if File.Exists path then path
        elif File.Exists(path + ".dashlayout") then path + ".dashlayout"
        else path

    let private resolveDiagramPath (path: string) =
        if File.Exists path then path
        elif File.Exists(path + ".dashdiagram") then path + ".dashdiagram"
        else path

    let private assignLayoutBoard (board: LayoutBoardDefinition) (moduleKind: DocumentModuleKind) (state: ModuleIncludeState) =
        match moduleKind with
        | DocumentModuleKind.Dashboard ->
            state.AssignToolbarBoard(board, "!include layout (dashboard toolbar)")
        | DocumentModuleKind.Tab ->
            state.AssignLayoutBoard(board, "!include layout (tab board)")

    let private registerDiagramFile (path: string) (specDirectory: string) (state: ModuleIncludeState) =
        let baseDirectory =
            Path.GetDirectoryName path |> Option.ofObj |> Option.defaultValue specDirectory

        let id, fragment =
            SpecIncludeFragmentResolver.foldDiagramModuleWithId (File.ReadAllText path) baseDirectory

        state.RegisterDiagram(id, fragment)

    let private registerPresentationFile (path: string) (specDirectory: string) (state: ModuleIncludeState) =
        let baseDirectory =
            Path.GetDirectoryName path |> Option.ofObj |> Option.defaultValue specDirectory

        let text = File.ReadAllText path
        let id, _ = PresentationModuleParser.parsePresentationModuleWithId text
        let block = SpecIncludeFragmentResolver.parsePresentationBlockFromFile text baseDirectory
        state.RegisterChartChromePreset(id, block)

    let private registerTooltipFile (path: string) (state: ModuleIncludeState) =
        let id, definition = TooltipModuleParser.parseTooltipFileWithId (File.ReadAllText path)
        state.RegisterTooltip(id, definition)

    let rec private expandDashInclude
        (path: string)
        (specDirectory: string)
        (moduleKind: DocumentModuleKind)
        (state: ModuleIncludeState)
        (tolerateIncompleteIncludes: bool)
        =
        let reader = ParserUtilities.createReader (File.ReadAllText path)
        reader.SkipNewlines()

        if reader.IsAt TokenKind.At then
            reader.Advance()

            if reader.TryKeyword "include" then
                reader.ReadIdent() |> ignore
                reader.SkipNewlines()

        while not reader.IsEof do
            match reader.TryModuleInclude() with
            | Some nested ->
                expand nested (Path.GetDirectoryName path |> Option.ofObj |> Option.defaultValue specDirectory) moduleKind state tolerateIncompleteIncludes
                reader.SkipNewlines()
            | None ->
                if reader.TryKeyword "layout" then
                    let layoutReference = reader.ReadString()
                    let layoutPath =
                        layoutReference
                        |> fun reference -> SpecIncludeResolver.resolvePath reference specDirectory
                        |> resolveLayoutPath

                    assignLayoutBoard (LayoutModuleParser.parseLayoutFile (File.ReadAllText layoutPath)) moduleKind state
                    reader.SkipNewlines()
                elif reader.TryKeyword "diagram" then
                    let diagramReference = reader.ReadString()

                    let diagramPath =
                        diagramReference
                        |> fun reference -> SpecIncludeResolver.resolvePath reference specDirectory
                        |> resolveDiagramPath

                    registerDiagramFile diagramPath specDirectory state
                    reader.SkipNewlines()
                else
                    raise (reader.Unexpected())

    and private expandFile
        (path: string)
        (specDirectory: string)
        (moduleKind: DocumentModuleKind)
        (state: ModuleIncludeState)
        (tolerateIncompleteIncludes: bool)
        =
        if not (File.Exists path) then
            raise (FileNotFoundException($"!include not found: '{path}'.", path))

        match Path.GetExtension(path).ToLowerInvariant() with
        | ".dashlayout" ->
            assignLayoutBoard (LayoutModuleParser.parseLayoutFile (File.ReadAllText path)) moduleKind state
        | ".dashdiagram" -> registerDiagramFile path specDirectory state
        | ".dashinclude" -> expandDashInclude path specDirectory moduleKind state tolerateIncompleteIncludes
        | ".dashpresentation" -> registerPresentationFile path specDirectory state
        | ".dashtooltip" -> registerTooltipFile path state
        | ".dashtransform" ->
            raise (DashSpecParseException($"!include '{path}': register transform via .dashdiagram or card block, not module include."))
        | extension ->
            raise (DashSpecParseException($"!include '{path}': unsupported extension '{extension}'."))

    and expand
        (reference: string)
        (specDirectory: string)
        (moduleKind: DocumentModuleKind)
        (state: ModuleIncludeState)
        (tolerateIncompleteIncludes: bool)
        =
        if String.IsNullOrWhiteSpace reference then
            invalidArg "reference" "Include reference is required."

        if String.IsNullOrWhiteSpace specDirectory then
            invalidArg "specDirectory" "Spec directory is required."

        if tolerateIncompleteIncludes && isIncomplete reference then
            ()
        else
            for path in resolvePaths reference specDirectory do
                expandFile path specDirectory moduleKind state tolerateIncompleteIncludes
