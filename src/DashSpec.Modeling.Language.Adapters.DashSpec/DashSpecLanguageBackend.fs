namespace DashSpec.Modeling.Language.Adapters.DashSpec

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open AIGuiders.Platform.Modeling.Language
open DashSpec.Core.Validation
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
                    Task.FromResult { Root = fileRoot path }
                elif String.Equals(Path.GetExtension path, ".dashspec", StringComparison.OrdinalIgnoreCase) then
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
                        Task.FromResult { Root = fileRoot path }
                else
                    Task.FromResult { Root = fileRoot path }

        member _.GoToDefinitionAsync(req, ct) =
            if ct.IsCancellationRequested then
                Task.FromCanceled<LanguageNavigation>(ct)
            elif not (isDashSpecPath req.FilePath)
                 || not (String.Equals(Path.GetExtension req.FilePath, ".dashspec", StringComparison.OrdinalIgnoreCase)) then
                Task.FromResult(emptyNavigation ())
            else
                let path = req.FilePath
                let text = readSource req

                match DashSpecSymbolIndex.goToDefinition path text req.Line req.Column with
                | None -> Task.FromResult(emptyNavigation ())
                | Some definition ->
                    Task.FromResult
                        { Definition = definition
                          Declarations = [| definition |] }

        member _.FindUsagesAsync(req, ct) =
            if ct.IsCancellationRequested then
                Task.FromCanceled<FindUsagesResult>(ct)
            elif not (isDashSpecPath req.FilePath)
                 || not (String.Equals(Path.GetExtension req.FilePath, ".dashspec", StringComparison.OrdinalIgnoreCase)) then
                Task.FromResult { References = [||] }
            else
                let path = req.FilePath
                let text = readSource req
                let references = DashSpecSymbolIndex.findReferences path text req.Line req.Column

                Task.FromResult { References = references }

        member _.GetCompletionsAsync(_req, ct) =
            if ct.IsCancellationRequested then
                Task.FromCanceled<CompletionsResult>(ct)
            else
                Task.FromResult { Items = [||] }

        member _.GetSymbolAtPositionAsync(req, ct) =
            if ct.IsCancellationRequested then
                Task.FromCanceled<SymbolAtPositionResult>(ct)
            elif not (isDashSpecPath req.FilePath)
                 || not (String.Equals(Path.GetExtension req.FilePath, ".dashspec", StringComparison.OrdinalIgnoreCase)) then
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
            elif not (isDashSpecPath renameReq.Request.FilePath)
                 || not (
                     String.Equals(
                         Path.GetExtension renameReq.Request.FilePath,
                         ".dashspec",
                         StringComparison.OrdinalIgnoreCase
                     )
                 ) then
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
