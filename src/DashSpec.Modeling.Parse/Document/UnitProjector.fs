namespace DashSpec.Modeling.Parse.Document

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Catalog
open DashSpec.Modeling.Parse.DataFlow
open DashSpec.Modeling.Parse.Diagram
open DashSpec.Modeling.Parse.Host
open DashSpec.Modeling.Parse.Layout
open DashSpec.Modeling.Parse.Palette
open DashSpec.Modeling.Parse.Presentation
open DashSpec.Modeling.Parse.Syntax
open DashSpec.Modeling.Parse.Tooltip
open DashSpec.Modeling.Parse.Transform
open DashSpec.Modeling.Parse.Types

/// <summary>Unified unit projector over the single DashSpec AST (<see cref="ParseTree"/> → <see cref="ProjectedUnit"/>).</summary>
[<RequireQualifiedAccess>]
type ProjectedUnit =
    | Types of id: string * ns: string option * definitions: RowTypeDef[]
    | Flow of moduleDef: DashflowModule
    | Layout of board: LayoutBoardDefinition
    | Presentation of id: string * doc: PresentationModuleDocument
    | Transform of id: string * block: SeriesTransformBlock
    | Palette of id: string * doc: PaletteDocument
    | Tooltip of id: string * def: TooltipDefinition
    | Catalog of doc: CatalogDocument
    | Host of doc: HostDocument
    | Diagram of id: string * doc: DiagramModuleDocument
    | Section of spec: DashboardDocument
    | Dashboard of spec: DashboardDocument
    | Custom of kind: string * id: string * payload: obj

module UnitProjector =

    let private customProjectors =
        System.Collections.Concurrent.ConcurrentDictionary<string, UnitGrammar.UnitEnvelope -> obj>(StringComparer.OrdinalIgnoreCase)

    let registerCustomProjector (kind: string) (projector: UnitGrammar.UnitEnvelope -> obj) =
        customProjectors.[kind] <- projector

    let project (text: string) (specDirectory: string option) (typeCatalog: TypeCatalog option) (parseOptions: DashSpecParseOptions option) : ProjectedUnit =
        if String.IsNullOrWhiteSpace text then
            invalidArg "text" "Unit text is required."

        let tree = DashSpecSurfaceSyntax.parse text

        match UnitGrammar.tryUnwrapAny tree.Root with
        | None ->
            raise (DashSpecUnitHeaderException("Unit requires 'module <kind> <id>'."))
        | Some env ->
            match env.Kind with
            | DashSpecModuleKind.Types ->
                let defs = TypeModuleParser.parseTypesModule text
                ProjectedUnit.Types(env.Id, env.Namespace, defs)
            | DashSpecModuleKind.Flow _ ->
                let catalog = defaultArg typeCatalog TypeCatalog.empty
                ProjectedUnit.Flow(DashflowModuleParser.parseModule text catalog)
            | DashSpecModuleKind.Layout ->
                ProjectedUnit.Layout(LayoutModuleParser.parseLayoutFile text)
            | DashSpecModuleKind.Presentation ->
                let doc = PresentationModuleParser.parsePresentationModule text
                ProjectedUnit.Presentation(doc.Id, doc)
            | DashSpecModuleKind.Transform ->
                let block = TransformModuleParser.parseTransformFile text
                ProjectedUnit.Transform(env.Id, block)
            | DashSpecModuleKind.Palette ->
                let doc = PaletteModuleParser.parsePaletteFile text
                ProjectedUnit.Palette(doc.Id, doc)
            | DashSpecModuleKind.Tooltip ->
                let def = TooltipModuleParser.parseTooltipFile text
                ProjectedUnit.Tooltip(def.Id, def)
            | DashSpecModuleKind.Catalog ->
                ProjectedUnit.Catalog(CatalogParser.parse text)
            | DashSpecModuleKind.Host ->
                ProjectedUnit.Host(HostModuleParser.parse text specDirectory)
            | DashSpecModuleKind.Diagram ->
                let id, doc = DiagramModuleParser.parseDiagramModuleWithId text
                ProjectedUnit.Diagram(id, doc)
            | DashSpecModuleKind.Section ->
                let opts = defaultArg parseOptions DashSpecParseOptions.defaultOptions
                ProjectedUnit.Section(DashboardComposer.parse text specDirectory opts)
            | DashSpecModuleKind.Dashboard ->
                let opts = defaultArg parseOptions DashSpecParseOptions.defaultOptions
                ProjectedUnit.Dashboard(DashboardComposer.parse text specDirectory opts)
            | DashSpecModuleKind.Custom kw ->
                match customProjectors.TryGetValue kw with
                | true, projector -> ProjectedUnit.Custom(kw, env.Id, projector env)
                | false, _ ->
                    raise (DashSpecUnitHeaderException($"Unknown module kind '{kw}'.", env.ModuleNode.Span.Start))
