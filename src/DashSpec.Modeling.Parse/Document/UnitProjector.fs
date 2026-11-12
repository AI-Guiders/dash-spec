namespace DashSpec.Modeling.Parse.Document

open System
open System.Collections.Concurrent
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

type UnitProjectionContext =
    { Text: string
      Envelope: UnitGrammar.UnitEnvelope
      SpecDirectory: string option
      TypeCatalog: TypeCatalog option
      ParseOptions: DashSpecParseOptions option }

type UnitProjectorDescriptor =
    { KindKeyword: string
      Project: UnitProjectionContext -> ProjectedUnit }

/// <summary>Plugin-driven unit projector registry: every unit kind (built-in or external plugin)
/// is registered as a <see cref="UnitProjectorDescriptor"/> keyed by its module keyword.</summary>
module UnitProjector =

    let private projectors =
        ConcurrentDictionary<string, UnitProjectorDescriptor>(StringComparer.OrdinalIgnoreCase)

    let register (descriptor: UnitProjectorDescriptor) =
        if not (String.IsNullOrWhiteSpace descriptor.KindKeyword) then
            projectors.[descriptor.KindKeyword.Trim()] <- descriptor

    let registerCustomProjector (kind: string) (projector: UnitGrammar.UnitEnvelope -> obj) =
        register
            { KindKeyword = kind
              Project = fun ctx -> ProjectedUnit.Custom(kind, ctx.Envelope.Id, projector ctx.Envelope) }

    do
        let builtins =
            [ { KindKeyword = "types"
                Project = fun ctx ->
                    let defs = TypeModuleParser.parseTypesModule ctx.Text
                    ProjectedUnit.Types(ctx.Envelope.Id, ctx.Envelope.Namespace, defs) }
              { KindKeyword = "flow"
                Project = fun ctx ->
                    let catalog = defaultArg ctx.TypeCatalog TypeCatalog.empty
                    ProjectedUnit.Flow(DashflowModuleParser.parseModule ctx.Text catalog) }
              { KindKeyword = "layout"
                Project = fun ctx ->
                    ProjectedUnit.Layout(LayoutModuleParser.parseLayoutFile ctx.Text) }
              { KindKeyword = "presentation"
                Project = fun ctx ->
                    let doc = PresentationModuleParser.parsePresentationModule ctx.Text
                    ProjectedUnit.Presentation(doc.Id, doc) }
              { KindKeyword = "transform"
                Project = fun ctx ->
                    let block = TransformModuleParser.parseTransformFile ctx.Text
                    ProjectedUnit.Transform(ctx.Envelope.Id, block) }
              { KindKeyword = "palette"
                Project = fun ctx ->
                    let doc = PaletteModuleParser.parsePaletteFile ctx.Text
                    ProjectedUnit.Palette(doc.Id, doc) }
              { KindKeyword = "tooltip"
                Project = fun ctx ->
                    let def = TooltipModuleParser.parseTooltipFile ctx.Text
                    ProjectedUnit.Tooltip(def.Id, def) }
              { KindKeyword = "catalog"
                Project = fun ctx ->
                    ProjectedUnit.Catalog(CatalogParser.parse ctx.Text) }
              { KindKeyword = "host"
                Project = fun ctx ->
                    ProjectedUnit.Host(HostModuleParser.parse ctx.Text ctx.SpecDirectory) }
              { KindKeyword = "diagram"
                Project = fun ctx ->
                    let id, doc = DiagramModuleParser.parseDiagramModuleWithId ctx.Text
                    ProjectedUnit.Diagram(id, doc) }
              { KindKeyword = "section"
                Project = fun ctx ->
                    let opts = defaultArg ctx.ParseOptions DashSpecParseOptions.defaultOptions
                    ProjectedUnit.Section(DashboardComposer.parse ctx.Text ctx.SpecDirectory opts) }
              { KindKeyword = "dashboard"
                Project = fun ctx ->
                    let opts = defaultArg ctx.ParseOptions DashSpecParseOptions.defaultOptions
                    ProjectedUnit.Dashboard(DashboardComposer.parse ctx.Text ctx.SpecDirectory opts) } ]

        for d in builtins do
            register d

    let project (text: string) (specDirectory: string option) (typeCatalog: TypeCatalog option) (parseOptions: DashSpecParseOptions option) : ProjectedUnit =
        if String.IsNullOrWhiteSpace text then
            invalidArg "text" "Unit text is required."

        let tree = DashSpecSurfaceSyntax.parse text

        match UnitGrammar.tryUnwrapAny tree.Root with
        | None ->
            raise (DashSpecUnitHeaderException("Unit requires 'module <kind> <id>'."))
        | Some env ->
            let kw = DashSpecModuleKind.toKeyword env.Kind
            match projectors.TryGetValue kw with
            | true, descriptor ->
                descriptor.Project
                    { Text = text
                      Envelope = env
                      SpecDirectory = specDirectory
                      TypeCatalog = typeCatalog
                      ParseOptions = parseOptions }
            | false, _ ->
                raise (DashSpecUnitHeaderException($"Unknown module kind '{kw}'.", env.ModuleNode.Span.Start))
