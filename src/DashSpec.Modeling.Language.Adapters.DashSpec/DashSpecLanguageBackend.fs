namespace DashSpec.Modeling.Language.Adapters.DashSpec

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open AIGuiders.Platform.Modeling.Language
open DashSpec.Compiler.Contract
open DashSpec.Execution.Runtime
open DashSpec.Execution.Compilation
open DashSpec.Execution.Parsing

/// <summary>LRC backend for DashSpec planet roots (GUIDERS-ADR-0061 planet extension).</summary>
type DashSpecLanguageBackend() =
    static let ensureRegistered () = DashSpecParser.EnsureModuleParsersRegistered()

    static let languageId = LanguageIds.Dashspec

    static let isDashSpecPath (path: string) = DashSpecPathRules.isDashSpecPath path

    static let readSource (req: LanguageRequest) =
        if not (String.IsNullOrWhiteSpace req.SourceText) then
            req.SourceText
        elif File.Exists req.FilePath then
            File.ReadAllText req.FilePath
        else
            ""

    static let workspaceIndex (req: LanguageRequest) =
        DashSpecEditorIntelligence.ResolveWorkspaceIndex(
            (if String.IsNullOrWhiteSpace req.SolutionOrProjectPath then null else req.SolutionOrProjectPath),
            req.FilePath
        )

    static let specDirectory (req: LanguageRequest) =
        let dir = Path.GetDirectoryName req.FilePath

        if String.IsNullOrWhiteSpace dir then
            "."
        else
            dir

    static let toSpan path line character endLine endCharacter =
        { Path = path
          Line = line
          Column = character
          EndLine = endLine
          EndColumn = endCharacter }

    static let toDiagnostic path (diag: DashSpecDiagnostic) =
        let severity =
            match diag.Severity with
            | DashSpecDiagnosticSeverity.Error -> Severity.Error
            | DashSpecDiagnosticSeverity.Warning -> Severity.Warning
            | DashSpecDiagnosticSeverity.Information -> Severity.Info
            | _ -> Severity.Error

        { Id = "DSPEC"
          Severity = severity
          Message = diag.Message
          Span = toSpan path diag.Line diag.Character diag.EndLine diag.EndCharacter
          Tags = [||]
          Language = languageId }

    static let emptyNavigation () = Unchecked.defaultof<LanguageNavigation>

    static let fileRoot (path: string) =
        { Name = Path.GetFileName path
          Kind = "file"
          Span = toSpan path 1 1 1 1
          Container = ""
          Children = [||] }

    static let namedSymbol (path: string) name kind container line =
        { Name = name
          Kind = kind
          Span = toSpan path line 1 line 1
          Container = container
          Children = [||] }

    /// Token-level outline for non-dashboard unit files (diagram / flow / presentation / …):
    /// block-level definitions from the shared token index, grouped by kind.
    static let tokenOutline (path: string) (text: string) =
        let defs = DashSpecSymbolIndex.collectDefinitions path text

        let children =
            defs
            |> Seq.distinctBy (fun d -> $"""{d.Kind}:{d.Name}""")
            |> Seq.groupBy (fun d -> d.Kind)
            |> Seq.collect (fun (kind, group) ->
                group
                |> Seq.map (fun d ->
                    { Name = d.Name
                      Kind = kind
                      Span = d.Span
                      Container = ""
                      Children = [||] }))
            |> Array.ofSeq

        { Name = Path.GetFileName path
          Kind = "file"
          Span = toSpan path 1 1 1 1
          Container = ""
          Children = children }

    do ensureRegistered ()

    interface ILanguageBackend with
        member _.LanguageId = languageId

        member _.CanHandle(path, _hint) = isDashSpecPath path

        member _.GetDiagnosticsAsync(req, ct) =
            if ct.IsCancellationRequested then
                Task.FromCanceled<DiagnosticsResult>(ct)
            elif not (isDashSpecPath req.FilePath) then
                Task.FromResult { Diagnostics = [||] }
            else
                let path = req.FilePath
                let text = readSource req
                let specDir = specDirectory req

                let diagnostics =
                    DashSpecDiagnosticService.ValidateText(text, path, specDir)
                    |> List.ofSeq
                    |> List.map (toDiagnostic path)
                    |> Array.ofList

                Task.FromResult { Diagnostics = diagnostics }

        member _.GetDocumentSymbolsAsync(req, ct) =
            if ct.IsCancellationRequested then
                Task.FromCanceled<DocumentSymbolsResult>(ct)
            elif not (isDashSpecPath req.FilePath) then
                Task.FromResult { Root = fileRoot req.FilePath }
            else
                let path = req.FilePath
                let text = readSource req
                let specDir = specDirectory req

                let diags = DashSpecDiagnosticService.ValidateText(text, path, specDir)

                if diags.Count > 0 then
                    Task.FromResult { Root = tokenOutline path text }
                elif String.Equals(Path.GetExtension path, ".ds", StringComparison.OrdinalIgnoreCase) then
                    try
                        let doc =
                            DashSpec.Modeling.Parse.Document.DashboardComposer.parse
                                text
                                (Some specDir)
                                DashSpec.Modeling.Parse.DashSpecParseOptions.defaultOptions

                        let filterChildren =
                            doc.Filters
                            |> Seq.mapi (fun i f -> namedSymbol path f.Name "filter" "filters" (i + 2))
                            |> Array.ofSeq

                        let cardChildren =
                            doc.Cards
                            |> Seq.mapi (fun i c -> namedSymbol path c.Id "card" "cards" (i + 2))
                            |> Array.ofSeq

                        let tabChildren =
                            doc.Tabs
                            |> Seq.mapi (fun i t -> namedSymbol path t.Id "tab" "tabs" (i + 2))
                            |> Array.ofSeq

                        let dashboardChild =
                            { Name = doc.Id
                              Kind = "dashboard"
                              Span = toSpan path 1 1 1 1
                              Container = ""
                              Children = Array.append (Array.append filterChildren cardChildren) tabChildren }

                        Task.FromResult
                            { Root =
                                { Name = Path.GetFileName path
                                  Kind = "file"
                                  Span = toSpan path 1 1 1 1
                                  Container = ""
                                  Children = [| dashboardChild |] } }
                    with _ ->
                        Task.FromResult { Root = tokenOutline path text }
                else
                    // Diagram / flow / presentation / … module files: token-level outline
                    // (module header + block-level definitions) — DashboardComposer is dashboard-only.
                    Task.FromResult { Root = tokenOutline path text }

        member _.GoToDefinitionAsync(req, ct) =
            if ct.IsCancellationRequested then
                Task.FromCanceled<LanguageNavigation>(ct)
            elif not (isDashSpecPath req.FilePath) then
                Task.FromResult(emptyNavigation ())
            else
                let path = req.FilePath
                let text = readSource req

                // Workspace-first (ADR-0101 agent authoring): `diagram ref <slot> <id>`,
                // `use flow <id>`, `import <kind> from <Ns>` resolve across the corpus
                // to the declaring module file; single-file token index as fallback.
                // F# view of the C# `DefinitionTarget?`: null-checked, not Option.
                let workspaceTarget =
                    match
                        DashSpecEditorIntelligence.TryGetDefinition(
                            path,
                            text,
                            req.Line,
                            req.Column,
                            workspaceIndex req
                        )
                    with
                    | null -> None
                    | t when File.Exists t.FilePath -> Some t
                    | _ -> None

                match workspaceTarget with
                | Some target ->
                    let span =
                        toSpan target.FilePath target.Line target.Column target.Line target.Column

                    Task.FromResult { Definition = span; Declarations = [| span |] }
                | _ ->
                    match DashSpecSymbolIndex.goToDefinition path text req.Line req.Column with
                    | None -> Task.FromResult(emptyNavigation ())
                    | Some definition ->
                        Task.FromResult
                            { Definition = definition
                              Declarations = [| definition |] }

        member _.FindUsagesAsync(req, ct) =
            if ct.IsCancellationRequested then
                Task.FromCanceled<FindUsagesResult>(ct)
            elif not (isDashSpecPath req.FilePath) then
                Task.FromResult { References = [||] }
            else
                let path = req.FilePath
                let text = readSource req

                // Workspace-first: a module id (diagram / flow / presentation / …)
                // finds its isolated-word usages across the corpus; single-file
                // token references as fallback (cards / filters / tabs stay local).
                match DashSpecSymbolIndex.resolveSymbolAt path text req.Line req.Column with
                | Some symbol ->
                    let index = workspaceIndex req
                    let usages = DashSpecEditorIntelligence.FindWorkspaceUsages(symbol.Name, index)

                    if usages.Count > 0 then
                        let kind =
                            match index.UnitsById.TryGetValue symbol.Name with
                            | true, unit -> $"module {unit.Kind}"
                            | false, _ -> symbol.Kind

                        let references =
                            usages
                            |> Seq.map (fun u ->
                                { Span = toSpan u.Path u.Line u.Column u.EndLine u.EndColumn
                                  Target = toSpan path symbol.Span.Line symbol.Span.Column symbol.Span.Line symbol.Span.Column
                                  Kind = kind })
                            |> Array.ofSeq

                        Task.FromResult { References = references }
                    else
                        let references = DashSpecSymbolIndex.findReferences path text req.Line req.Column
                        Task.FromResult { References = references }
                | None ->
                    let references = DashSpecSymbolIndex.findReferences path text req.Line req.Column
                    Task.FromResult { References = references }

        member _.GetCompletionsAsync(req, ct) =
            if ct.IsCancellationRequested then
                Task.FromCanceled<CompletionsResult>(ct)
            elif not (isDashSpecPath req.FilePath) then
                Task.FromResult { Items = [||] }
            else
                let path = req.FilePath
                let text = readSource req
                let index =
                    DashSpecEditorIntelligence.ResolveWorkspaceIndex(req.SolutionOrProjectPath, path)

                let suggestions =
                    DashSpecEditorIntelligence.GetCompletions(path, text, req.Line, req.Column, index)

                let items =
                    suggestions
                    |> Seq.map (fun s ->
                        { Label = s.Label
                          Kind = if isNull s.Kind then "keyword" else s.Kind
                          Detail = if isNull s.Detail then "" else s.Detail
                          InsertText = s.InsertText })
                    |> Array.ofSeq

                Task.FromResult { Items = items }

        member _.GetSymbolAtPositionAsync(req, ct) =
            if ct.IsCancellationRequested then
                Task.FromCanceled<SymbolAtPositionResult>(ct)
            elif not (isDashSpecPath req.FilePath) then
                Task.FromResult(Unchecked.defaultof<SymbolAtPositionResult>)
            else
                let path = req.FilePath
                let text = readSource req

                match DashSpecSymbolIndex.resolveSymbolAt path text req.Line req.Column with
                | None -> Task.FromResult(Unchecked.defaultof<SymbolAtPositionResult>)
                | Some symbol ->
                    Task.FromResult
                        { Kind = symbol.Kind
                          Name = symbol.Name
                          QualifiedName = symbol.QualifiedName
                          Span = symbol.Span }

        member _.RenameSymbolAsync(renameReq, ct) =
            if ct.IsCancellationRequested then
                Task.FromCanceled<RenameSymbolResult>(ct)
            elif not (isDashSpecPath renameReq.Request.FilePath) then
                Task.FromResult
                    { OldName = ""
                      NewName = renameReq.NewName
                      SymbolKind = ""
                      Applied = false
                      Message = ""
                      Files = [||]
                      Changes = [||] }
            elif String.IsNullOrWhiteSpace renameReq.NewName then
                Task.FromResult
                    { OldName = ""
                      NewName = renameReq.NewName
                      SymbolKind = ""
                      Applied = false
                      Message = "new_name is required."
                      Files = [||]
                      Changes = [||] }
            else
                let req = renameReq.Request
                let path = req.FilePath
                let text = readSource req

                match DashSpecSymbolIndex.resolveSymbolAt path text req.Line req.Column with
                | None ->
                    Task.FromResult
                        { OldName = ""
                          NewName = renameReq.NewName
                          SymbolKind = ""
                          Applied = false
                          Message = "No symbol at position."
                          Files = [||]
                          Changes = [||] }
                | Some symbol when symbol.Kind = "reference" ->
                    Task.FromResult
                        { OldName = symbol.Name
                          NewName = renameReq.NewName
                          SymbolKind = symbol.Kind
                          Applied = false
                          Message = "Rename is only supported for declared card, filter, tab, dashboard, and diagram ids."
                          Files = [||]
                          Changes = [||] }
                | Some symbol ->
                    let newText =
                        System.Text.RegularExpressions.Regex.Replace(
                            text,
                            $"\\b{System.Text.RegularExpressions.Regex.Escape symbol.Name}\\b",
                            renameReq.NewName,
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase
                        )

                    let changes =
                        if renameReq.Apply && not (String.Equals(text, newText, StringComparison.Ordinal)) then
                            if File.Exists path then File.WriteAllText(path, newText)

                            [| { Path = path; NewText = newText } |]
                        else
                            [| { Path = path; NewText = newText } |]

                    Task.FromResult
                        { OldName = symbol.Name
                          NewName = renameReq.NewName
                          SymbolKind = symbol.Kind
                          Applied =
                              renameReq.Apply
                              && not (String.Equals(text, newText, StringComparison.Ordinal))
                          Message = ""
                          Files = [| path |]
                          Changes = changes }
