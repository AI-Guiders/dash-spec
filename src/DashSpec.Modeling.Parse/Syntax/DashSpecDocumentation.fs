namespace DashSpec.Modeling.Parse.Syntax

open System
open System.Collections.Generic
open System.Text.RegularExpressions
open DashSpec.Modeling.Core

/// <summary>
/// Typed documentation node extracted from <c>///</c> doc comments and AST structure
/// during compilation (DASHSPEC-ADR-0138).
/// </summary>
[<CLIMutable>]
type DashSpecSymbolDoc =
    { Kind: string
      Id: string
      Title: string option
      Namespace: string option
      Signature: string
      Markdown: string
      SourcePath: string option
      Line: int
      ReferencedSymbols: string[]
      Children: DashSpecSymbolDoc[] }

module DashSpecDocumentationExtractor =

    let private textRefPattern =
        Regex(@"@text\(\s*([A-Za-z0-9_.\-]+)\s*\)", RegexOptions.Compiled ||| RegexOptions.CultureInvariant)

    let private lineOfOffset (text: string) (offset: int) : int =
        TextSpan.lineOfOffset text offset

    /// Resolve `@text(<key>)` placeholders and `<kind>.<id>.doc` / `<id>.doc` overrides from `module texts`.
    let resolveLocalizedMarkdown
        (tryResolveText: string -> string option)
        (kind: string)
        (symbolId: string)
        (parentId: string option)
        (rawMarkdown: string)
        : string =
        let candidates =
            [ match parentId with
              | Some p when not (String.IsNullOrWhiteSpace p) ->
                  yield $"{kind}.{p}.{symbolId}.doc"
                  yield $"{p}.{symbolId}.doc"
              | _ -> ()
              yield $"{kind}.{symbolId}.doc"
              yield $"{symbolId}.doc" ]

        let baseText =
            candidates
            |> List.tryPick tryResolveText
            |> Option.defaultValue rawMarkdown

        if String.IsNullOrWhiteSpace baseText || not (baseText.Contains("@text(", StringComparison.OrdinalIgnoreCase)) then
            baseText
        else
            textRefPattern.Replace(
                baseText,
                MatchEvaluator(fun m ->
                    let key = m.Groups.[1].Value.Trim()
                    match tryResolveText key with
                    | Some resolved -> resolved
                    | None -> m.Value))

    let private nodeDoc (docs: IReadOnlyDictionary<AIGuiders.Platform.Modeling.Core.Identity.NodeId, string>) (nodeId: AIGuiders.Platform.Modeling.Core.Identity.NodeId) =
        match docs.TryGetValue nodeId with
        | true, md -> md
        | false, _ -> ""

    let private formatDashType (dt: DashType) =
        match dt with
        | DashType.Primitive p -> DashPrimitive.toString p
        | DashType.Named name -> name
        | DashType.FixedArray(el, len) -> $"{DashPrimitive.toString el}[{len}]"

    let private collectPropertyRefs (v: PropertyValueSyntax) : string[] =
        match v with
        | PropertyValueSyntax.Member d -> [| d.Text |]
        | PropertyValueSyntax.Aliased(inner, _) ->
            match inner with
            | PropertyValueSyntax.Member d -> [| d.Text |]
            | _ -> [||]
        | _ -> [||]

    let private collectBlockRefs (members: DashSpecAstNode[]) (typeAnnotation: string option) : string[] =
        let refs = ResizeArray<string>()
        match typeAnnotation with
        | Some t when not (String.IsNullOrWhiteSpace t) -> refs.Add t
        | _ -> ()
        for m in members do
            match m with
            | DashSpecAstNode.ClauseDeclaration c ->
                match c.Kind with
                | ClauseKind.UsePresetClause target -> refs.Add target
                | ClauseKind.UseClause(_, target) -> refs.Add target
                | ClauseKind.DirectiveClause("rows", args) when args.Length > 0 -> refs.Add args.[0]
                | _ -> ()
            | DashSpecAstNode.PropertyAssignment p ->
                for r in collectPropertyRefs p.Value do
                    refs.Add r
            | DashSpecAstNode.BlockDeclaration b ->
                match b.Opener with
                | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Transform, pluginId) -> refs.Add pluginId
                | _ -> ()
            | _ -> ()
        refs |> Seq.distinctBy (fun s -> s.ToLowerInvariant()) |> Seq.toArray

    let rec private extractBlockChildren
        (text: string)
        (sourcePath: string option)
        (ns: string option)
        (docs: IReadOnlyDictionary<AIGuiders.Platform.Modeling.Core.Identity.NodeId, string>)
        (tryResolveText: string -> string option)
        (parentKind: string)
        (parentId: string)
        (members: DashSpecAstNode[])
        : DashSpecSymbolDoc[] =
        let children = ResizeArray<DashSpecSymbolDoc>()
        for m in members do
            match m with
            | DashSpecAstNode.TypeFieldDeclaration f ->
                let optPrefix = if f.Optional then "optional " else ""
                let typeName = formatDashType f.FieldType
                let fieldRefs =
                    match f.FieldType with
                    | DashType.Named name -> [| name |]
                    | _ -> [||]
                let sigText = $"{optPrefix}{typeName} {f.FieldName}"
                let rawMd = nodeDoc docs f.Id
                let md = resolveLocalizedMarkdown tryResolveText "field" f.FieldName (Some parentId) rawMd
                children.Add
                    { Kind = "field"
                      Id = f.FieldName
                      Title = None
                      Namespace = ns
                      Signature = sigText
                      Markdown = md
                      SourcePath = sourcePath
                      Line = lineOfOffset text f.Span.Start
                      ReferencedSymbols = fieldRefs
                      Children = [||] }
            | DashSpecAstNode.PortDeclaration p ->
                let roleStr =
                    match p.Role with
                    | PortDeclRole.PortInput -> "input"
                    | PortDeclRole.PortOutput -> "output"
                    | PortDeclRole.PortParam -> "param"
                let shapeStr =
                    match p.PortShape with
                    | Some PortShapeSyntax.Stream -> " stream"
                    | Some PortShapeSyntax.Scalar -> " scalar"
                    | None -> ""
                let typeStr = p.ValueType |> Option.map (fun t -> $": {t}") |> Option.defaultValue ""
                let portRefs = p.ValueType |> Option.map (fun t -> [| t |]) |> Option.defaultValue [||]
                let sigText = $"{roleStr}{shapeStr} {p.PortName}{typeStr}"
                let rawMd = nodeDoc docs p.Id
                let md = resolveLocalizedMarkdown tryResolveText "port" p.PortName (Some parentId) rawMd
                children.Add
                    { Kind = "port"
                      Id = p.PortName
                      Title = None
                      Namespace = ns
                      Signature = sigText
                      Markdown = md
                      SourcePath = sourcePath
                      Line = lineOfOffset text p.Span.Start
                      ReferencedSymbols = portRefs
                      Children = [||] }
            | DashSpecAstNode.PropertyAssignment p ->
                let rawMd = nodeDoc docs p.Id
                let isParam =
                    p.KeyTokens.Length = 2
                    && String.Equals(p.KeyTokens.[0].Text, "param", StringComparison.OrdinalIgnoreCase)
                if isParam || not (String.IsNullOrWhiteSpace rawMd) then
                    let propId = if isParam then p.KeyTokens.[1].Text else p.Key
                    let propKind = if isParam then "param" else "property"
                    let sigText = $"{p.Key} = {p.ValueText}"
                    let md = resolveLocalizedMarkdown tryResolveText propKind propId (Some parentId) rawMd
                    children.Add
                        { Kind = propKind
                          Id = propId
                          Title = None
                          Namespace = ns
                          Signature = sigText
                          Markdown = md
                          SourcePath = sourcePath
                          Line = lineOfOffset text p.Span.Start
                          ReferencedSymbols = collectPropertyRefs p.Value
                          Children = [||] }
            | DashSpecAstNode.BlockDeclaration innerBlock ->
                match innerBlock.Opener with
                | DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Ports
                | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Ports, _) ->
                    for portDoc in extractBlockChildren text sourcePath ns docs tryResolveText parentKind parentId innerBlock.Members do
                        children.Add portDoc
                | DashSpecBlockOpener.Named(kw, childId) ->
                    let childKind = DashSpecBlockKeyword.toEndName kw
                    let title = DashSpecAst.tryTitle m
                    let rawMd = nodeDoc docs innerBlock.Id
                    let md = resolveLocalizedMarkdown tryResolveText childKind childId (Some parentId) rawMd
                    let grandChildren = extractBlockChildren text sourcePath ns docs tryResolveText childKind childId innerBlock.Members
                    children.Add
                        { Kind = childKind
                          Id = childId
                          Title = title
                          Namespace = ns
                          Signature = $"{childKind} {childId}"
                          Markdown = md
                          SourcePath = sourcePath
                          Line = lineOfOffset text innerBlock.Span.Start
                          ReferencedSymbols = collectBlockRefs innerBlock.Members innerBlock.TypeAnnotation
                          Children = grandChildren }
                | DashSpecBlockOpener.Anonymous kw ->
                    let childKind = DashSpecBlockKeyword.toEndName kw
                    let rawMd = nodeDoc docs innerBlock.Id
                    if not (String.IsNullOrWhiteSpace rawMd) then
                        children.Add
                            { Kind = childKind
                              Id = childKind
                              Title = None
                              Namespace = ns
                              Signature = childKind
                              Markdown = rawMd
                              SourcePath = sourcePath
                              Line = lineOfOffset text innerBlock.Span.Start
                              ReferencedSymbols = collectBlockRefs innerBlock.Members innerBlock.TypeAnnotation
                              Children = [||] }
            | DashSpecAstNode.TableRowDeclaration row ->
                let rawMd = nodeDoc docs row.Id
                match row.Kind with
                | TableRowKind.Header _
                | TableRowKind.Separator -> ()
                | _ when row.Cells.Length > 0 && (not (String.IsNullOrWhiteSpace rawMd) || parentKind = "registry") ->
                    let rowId = row.Cells.[0].Trim()
                    let sigText = "| " + String.Join(" | ", row.Cells) + " |"
                    let md = resolveLocalizedMarkdown tryResolveText parentKind rowId (Some parentId) rawMd
                    children.Add
                        { Kind = "row"
                          Id = rowId
                          Title = if row.Cells.Length > 1 then Some(row.Cells.[1].Trim()) else None
                          Namespace = ns
                          Signature = sigText
                          Markdown = md
                          SourcePath = sourcePath
                          Line = lineOfOffset text row.Span.Start
                          ReferencedSymbols = [||]
                          Children = [||] }
                | _ -> ()
            | _ -> ()
        children.ToArray()

    /// Extract structured <see cref="DashSpecSymbolDoc"/> tree from a parsed <see cref="ParseTree"/>.
    let extractFromTree
        (sourcePath: string option)
        (tryResolveText: string -> string option)
        (tree: ParseTree)
        : DashSpecSymbolDoc[] =
        let docs = DashSpecAst.collectLeadingDocs tree.Root
        let results = ResizeArray<DashSpecSymbolDoc>()

        for topNode in DashSpecAst.members tree.Root do
            match topNode with
            | DashSpecAstNode.ModuleDeclaration m ->
                let unitKind, unitId =
                    match m.Directive with
                    | DashSpecModuleDirective.Dashboard id -> "dashboard", id
                    | DashSpecModuleDirective.Tab id -> "section", id
                    | DashSpecModuleDirective.Module(k, id) -> DashSpecModuleKind.toKeyword k, id

                let ns =
                    m.Members
                    |> Array.tryPick (function
                        | DashSpecAstNode.ClauseDeclaration { Kind = ClauseKind.NamespaceClause n } -> Some n
                        | _ -> None)

                let rawModuleMd = nodeDoc docs m.Id
                let moduleMd = resolveLocalizedMarkdown tryResolveText unitKind unitId None rawModuleMd

                let rec collectNamedBlocks (nodes: DashSpecAstNode[]) (acc: ResizeArray<DashSpecSymbolDoc>) =
                    for node in nodes do
                        match node with
                        | DashSpecAstNode.BlockDeclaration b ->
                            match b.Opener with
                            | DashSpecBlockOpener.Named(kw, blockId) ->
                                let bKind = DashSpecBlockKeyword.toEndName kw
                                let bTitle = DashSpecAst.tryTitle node
                                let rawBMd = nodeDoc docs b.Id
                                let bMd = resolveLocalizedMarkdown tryResolveText bKind blockId (Some unitId) rawBMd
                                let bChildren = extractBlockChildren tree.Text sourcePath ns docs tryResolveText bKind blockId b.Members
                                let sigText =
                                    match bTitle with
                                    | Some t -> $"{bKind} {blockId} as \"{t}\""
                                    | None -> $"{bKind} {blockId}"
                                acc.Add
                                    { Kind = bKind
                                      Id = blockId
                                      Title = bTitle
                                      Namespace = ns
                                      Signature = sigText
                                      Markdown = bMd
                                      SourcePath = sourcePath
                                      Line = lineOfOffset tree.Text b.Span.Start
                                      ReferencedSymbols = collectBlockRefs b.Members b.TypeAnnotation
                                      Children = bChildren }
                            | DashSpecBlockOpener.Anonymous kw ->
                                let bKind = DashSpecBlockKeyword.toEndName kw
                                // Traverse inside `report`, `connect`, etc. to find cards/filters/pages
                                if kw = DashSpecBlockKeyword.Report || kw = DashSpecBlockKeyword.Connect then
                                    collectNamedBlocks b.Members acc
                                else
                                    let rawBMd = nodeDoc docs b.Id
                                    if not (String.IsNullOrWhiteSpace rawBMd) then
                                        acc.Add
                                            { Kind = bKind
                                              Id = bKind
                                              Title = None
                                              Namespace = ns
                                              Signature = bKind
                                              Markdown = rawBMd
                                              SourcePath = sourcePath
                                              Line = lineOfOffset tree.Text b.Span.Start
                                              ReferencedSymbols = collectBlockRefs b.Members b.TypeAnnotation
                                              Children = [||] }
                        | _ -> ()

                let moduleChildren = ResizeArray<DashSpecSymbolDoc>()
                collectNamedBlocks m.Members moduleChildren
                for directChild in extractBlockChildren tree.Text sourcePath ns docs tryResolveText unitKind unitId m.Members do
                    if not (moduleChildren |> Seq.exists (fun c -> c.Kind = directChild.Kind && String.Equals(c.Id, directChild.Id, StringComparison.OrdinalIgnoreCase))) then
                        moduleChildren.Add directChild

                let moduleTitle =
                    m.Members
                    |> Array.tryPick (function
                        | DashSpecAstNode.BlockDeclaration b when b.Opener = DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Report ->
                            DashSpecAst.tryTitle (DashSpecAstNode.BlockDeclaration b)
                        | _ -> None)

                results.Add
                    { Kind = $"module {unitKind}"
                      Id = unitId
                      Title = moduleTitle
                      Namespace = ns
                      Signature =
                          match ns with
                          | Some n -> $"module {unitKind} {unitId} (namespace {n})"
                          | None -> $"module {unitKind} {unitId}"
                      Markdown = moduleMd
                      SourcePath = sourcePath
                      Line = lineOfOffset tree.Text m.Span.Start
                      ReferencedSymbols = collectBlockRefs m.Members None
                      Children = moduleChildren.ToArray() }
            | _ -> ()

        results.ToArray()

    /// Extract structured <see cref="DashSpecSymbolDoc"/> tree from raw `.ds` text.
    let extractFromText
        (sourcePath: string option)
        (tryResolveText: string -> string option)
        (text: string)
        : DashSpecSymbolDoc[] =
        if String.IsNullOrWhiteSpace text then
            [||]
        else
            let tree = SyntaxTree.parse text
            extractFromTree sourcePath tryResolveText tree

    /// Flatten a <see cref="DashSpecSymbolDoc"/> hierarchy into a symbol dictionary (`Id -> DashSpecSymbolDoc` and `Kind.Id -> DashSpecSymbolDoc`).
    let indexBySymbol (docs: DashSpecSymbolDoc seq) : IReadOnlyDictionary<string, DashSpecSymbolDoc> =
        let map = Dictionary<string, DashSpecSymbolDoc>(StringComparer.OrdinalIgnoreCase)
        let rec visit (d: DashSpecSymbolDoc) =
            if not (String.IsNullOrWhiteSpace d.Id) then
                if not (map.ContainsKey d.Id) || not (String.IsNullOrWhiteSpace d.Markdown) then
                    map.[d.Id] <- d
                let shortKind =
                    if d.Kind.StartsWith("module ", StringComparison.OrdinalIgnoreCase) then
                        d.Kind.Substring(7).Trim()
                    else
                        d.Kind
                map.[$"{shortKind}.{d.Id}"] <- d
            for child in d.Children do
                visit child
        for d in docs do
            visit d
        map :> IReadOnlyDictionary<_, _>

/// <summary>
/// First-class domain partition for <c>symbols = ...</c> in <c>module documentation &lt;id&gt;</c> (DASHSPEC-ADR-0138).
/// </summary>
[<RequireQualifiedAccess>]
type DocumentationSymbolsCategory =
    | Units
    | Transforms
    | Presentations
    | Types
    | Monitoring
    | Rules
    | Workspace
    | All

module DocumentationSymbolsCategory =

    let tryParse (raw: string) : DocumentationSymbolsCategory option =
        match raw.Trim().ToLowerInvariant() with
        | "units" -> Some DocumentationSymbolsCategory.Units
        | "transforms" -> Some DocumentationSymbolsCategory.Transforms
        | "presentations" -> Some DocumentationSymbolsCategory.Presentations
        | "types" -> Some DocumentationSymbolsCategory.Types
        | "monitoring" -> Some DocumentationSymbolsCategory.Monitoring
        | "rules" -> Some DocumentationSymbolsCategory.Rules
        | "workspace" -> Some DocumentationSymbolsCategory.Workspace
        | "all" -> Some DocumentationSymbolsCategory.All
        | _ -> None

    let toCategoryId (cat: DocumentationSymbolsCategory) : string =
        match cat with
        | DocumentationSymbolsCategory.Units -> "units"
        | DocumentationSymbolsCategory.Transforms -> "transforms"
        | DocumentationSymbolsCategory.Presentations -> "presentations"
        | DocumentationSymbolsCategory.Types -> "types"
        | DocumentationSymbolsCategory.Monitoring -> "monitoring"
        | DocumentationSymbolsCategory.Rules -> "rules"
        | DocumentationSymbolsCategory.Workspace -> "workspace"
        | DocumentationSymbolsCategory.All -> "all"

/// <summary>
/// One chapter/page in a <c>module documentation &lt;id&gt;</c> book (DASHSPEC-ADR-0138).
/// Combines authored <c>///</c> Markdown narrative, sub-sections, Doxygen symbol-domain tables
/// (<c>symbols = units | transforms | presentations | types | monitoring | rules | workspace</c>),
/// and live embedded <c>.ds</c> reports (<c>report = "..."</c>).
/// </summary>
[<CLIMutable>]
type DocumentationPageDefinition =
    { Id: string
      Title: string
      Markdown: string
      SymbolsCategory: DocumentationSymbolsCategory option
      EmbeddedReportUnit: string option
      Sections: DocumentationPageDefinition[] }

/// <summary>
/// First-class <c>module documentation &lt;id&gt;</c> compilation unit (DASHSPEC-ADR-0138).
/// </summary>
[<CLIMutable>]
type DocumentationModuleDocument =
    { Id: string
      Namespace: string option
      Title: string
      Markdown: string
      Pages: DocumentationPageDefinition[] }

module DocumentationModuleParser =

    let rec private parsePageBlock
        (docs: IReadOnlyDictionary<AIGuiders.Platform.Modeling.Core.Identity.NodeId, string>)
        (block: BlockDeclarationSyntax)
        (pageId: string)
        : DocumentationPageDefinition =
        let headerTitle =
            DashSpecAst.tryTitle (DashSpecAstNode.BlockDeclaration block)
        let mutable propTitle: string option = None
        let mutable symbolsCat: DocumentationSymbolsCategory option = None
        let mutable embeddedReport: string option = None
        let subSections = ResizeArray<DocumentationPageDefinition>()

        for m in block.Members do
            match m with
            | DashSpecAstNode.PropertyAssignment p ->
                match p.Key.Trim().ToLowerInvariant() with
                | "title" | "label" -> propTitle <- Some p.Value.Text
                | "symbols" | "category" -> symbolsCat <- DocumentationSymbolsCategory.tryParse p.Value.Text
                | "report" | "embed" -> embeddedReport <- Some p.Value.Text
                | _ -> ()
            | DashSpecAstNode.BlockDeclaration childBlock ->
                match childBlock.Opener with
                | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Tab, secId)
                | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Page, secId)
                | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Group, secId)
                | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Card, secId) ->
                    subSections.Add(parsePageBlock docs childBlock secId)
                | _ -> ()
            | _ -> ()

        let md =
            match docs.TryGetValue block.Id with
            | true, d -> d
            | false, _ -> ""

        { Id = pageId
          Title = headerTitle |> Option.orElse propTitle |> Option.defaultValue pageId
          Markdown = md
          SymbolsCategory = symbolsCat
          EmbeddedReportUnit = embeddedReport
          Sections = subSections.ToArray() }

    let parse (text: string) : DocumentationModuleDocument =
        if String.IsNullOrWhiteSpace text then
            invalidArg "text" "Documentation module text is required."

        let tree = SyntaxTree.parse text
        let docs = DashSpecAst.collectLeadingDocs tree.Root

        let moduleNode =
            DashSpecAst.members tree.Root
            |> Array.tryPick (function
                | DashSpecAstNode.ModuleDeclaration m -> Some m
                | _ -> None)
            |> Option.defaultWith (fun () ->
                raise (DashSpecParseException("Expected 'module documentation <id>'.")))

        let unitId =
            match moduleNode.Directive with
            | DashSpecModuleDirective.Module(DashSpecModuleKind.Documentation, id) -> id
            | DashSpecModuleDirective.Module(DashSpecModuleKind.Custom kw, id)
                when String.Equals(kw, "documentation", StringComparison.OrdinalIgnoreCase) -> id
            | _ ->
                raise (DashSpecParseException("Expected 'module documentation <id>'."))

        let mutable ns: string option = None
        let mutable title = unitId
        let pages = ResizeArray<DocumentationPageDefinition>()

        for m in moduleNode.Members do
            match m with
            | DashSpecAstNode.ClauseDeclaration { Kind = ClauseKind.NamespaceClause n } ->
                ns <- Some n
            | DashSpecAstNode.PropertyAssignment p when String.Equals(p.Key, "title", StringComparison.OrdinalIgnoreCase) ->
                title <- p.Value.Text
            | DashSpecAstNode.BlockDeclaration b ->
                match b.Opener with
                | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Page, pageId)
                | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Tab, pageId) ->
                    pages.Add(parsePageBlock docs b pageId)
                | _ -> ()
            | _ -> ()

        let moduleMd =
            match docs.TryGetValue moduleNode.Id with
            | true, d -> d
            | false, _ -> ""

        { Id = unitId
          Namespace = ns
          Title = title
          Markdown = moduleMd
          Pages = pages.ToArray() }
