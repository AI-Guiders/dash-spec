namespace DashSpec.Modeling.Parse.Diagram

open System
open System.Collections.Concurrent
open System.Collections.Generic
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

module DiagramKindRegistry =

    type PropertySpec = PropertySchemas.PropertySpec

    type DiagramDataFamily =
        | Chart = 0
        | Table = 1
        | Scalar = 2
        | Matrix = 3
        | Gantt = 4

    type DiagramKindSpec =
        { Id: string
          DataFamily: DiagramDataFamily
          Properties: PropertySpec list
          /// Roles a diagram of this kind cannot render without (role vocabulary:
          /// x/y/value/from/to — resolved through the role→key mapping at the
          /// compile-time binding check). Empty = no mandatory bindings.
          RequiredRoles: string list
          SupportsTopLimit: bool
          AllowExtensionProperties: bool }

    let private chartProperties = PropertySchemas.chartDiagram

    let private tableProperties = PropertySchemas.tableDiagram

    let private numberProperties = PropertySchemas.numberDiagram

    let private heatmapProperties = PropertySchemas.heatmapDiagram

    let private ganttProperties = PropertySchemas.ganttDiagram

    let private chartRequired = [ "x"; "y" ]

    let private specs =
        let d = ConcurrentDictionary<string, DiagramKindSpec>(StringComparer.OrdinalIgnoreCase)
        let builtins =
            [ "line", { Id = "line"; DataFamily = DiagramDataFamily.Chart; Properties = chartProperties; RequiredRoles = chartRequired; SupportsTopLimit = false; AllowExtensionProperties = true }
              "area", { Id = "area"; DataFamily = DiagramDataFamily.Chart; Properties = chartProperties; RequiredRoles = chartRequired; SupportsTopLimit = false; AllowExtensionProperties = true }
              "sparkline", { Id = "sparkline"; DataFamily = DiagramDataFamily.Chart; Properties = chartProperties; RequiredRoles = chartRequired; SupportsTopLimit = false; AllowExtensionProperties = true }
              "bar", { Id = "bar"; DataFamily = DiagramDataFamily.Chart; Properties = chartProperties; RequiredRoles = chartRequired; SupportsTopLimit = true; AllowExtensionProperties = true }
              "pie", { Id = "pie"; DataFamily = DiagramDataFamily.Chart; Properties = chartProperties; RequiredRoles = chartRequired; SupportsTopLimit = true; AllowExtensionProperties = true }
              "donut", { Id = "donut"; DataFamily = DiagramDataFamily.Chart; Properties = chartProperties; RequiredRoles = chartRequired; SupportsTopLimit = true; AllowExtensionProperties = true }
              "doughnut", { Id = "doughnut"; DataFamily = DiagramDataFamily.Chart; Properties = chartProperties; RequiredRoles = chartRequired; SupportsTopLimit = true; AllowExtensionProperties = true }
              "scatter", { Id = "scatter"; DataFamily = DiagramDataFamily.Chart; Properties = chartProperties; RequiredRoles = chartRequired; SupportsTopLimit = false; AllowExtensionProperties = true }
              "histogram", { Id = "histogram"; DataFamily = DiagramDataFamily.Chart; Properties = chartProperties; RequiredRoles = [ "value" ]; SupportsTopLimit = false; AllowExtensionProperties = true }
              "box", { Id = "box"; DataFamily = DiagramDataFamily.Chart; Properties = chartProperties; RequiredRoles = [ "value" ]; SupportsTopLimit = false; AllowExtensionProperties = true }
              "boxplot", { Id = "boxplot"; DataFamily = DiagramDataFamily.Chart; Properties = chartProperties; RequiredRoles = [ "value" ]; SupportsTopLimit = false; AllowExtensionProperties = true }
              "treemap", { Id = "treemap"; DataFamily = DiagramDataFamily.Chart; Properties = chartProperties; RequiredRoles = chartRequired; SupportsTopLimit = true; AllowExtensionProperties = true }
              "gauge", { Id = "gauge"; DataFamily = DiagramDataFamily.Chart; Properties = chartProperties; RequiredRoles = [ "value" ]; SupportsTopLimit = false; AllowExtensionProperties = true }
              "windrose", { Id = "windrose"; DataFamily = DiagramDataFamily.Chart; Properties = chartProperties; RequiredRoles = chartRequired; SupportsTopLimit = true; AllowExtensionProperties = true }
              "wind_rose", { Id = "wind_rose"; DataFamily = DiagramDataFamily.Chart; Properties = chartProperties; RequiredRoles = chartRequired; SupportsTopLimit = true; AllowExtensionProperties = true }
              "table", { Id = "table"; DataFamily = DiagramDataFamily.Table; Properties = tableProperties; RequiredRoles = []; SupportsTopLimit = true; AllowExtensionProperties = false }
              "number", { Id = "number"; DataFamily = DiagramDataFamily.Scalar; Properties = numberProperties; RequiredRoles = [ "value" ]; SupportsTopLimit = false; AllowExtensionProperties = false }
              "heatmap", { Id = "heatmap"; DataFamily = DiagramDataFamily.Matrix; Properties = heatmapProperties; RequiredRoles = [ "x"; "y"; "value" ]; SupportsTopLimit = false; AllowExtensionProperties = true }
              "gantt", { Id = "gantt"; DataFamily = DiagramDataFamily.Gantt; Properties = ganttProperties; RequiredRoles = [ "y"; "from"; "to" ]; SupportsTopLimit = true; AllowExtensionProperties = true } ]
        for k, v in builtins do
            d.[k] <- v
        d

    let register (spec: DiagramKindSpec) =
        if not (String.IsNullOrWhiteSpace spec.Id) then
            specs.[spec.Id] <- spec
            DashSpecKeywords.registerDiagramKind spec.Id

    let registerPluginKind (id: string) (dataFamily: string) (supportsTopLimit: bool) (bindingProps: string seq) =
        if not (String.IsNullOrWhiteSpace id) && not (specs.ContainsKey id) then
            let family, baseProps =
                match dataFamily.Trim().ToLowerInvariant() with
                | "table" -> DiagramDataFamily.Table, tableProperties
                | "scalar" -> DiagramDataFamily.Scalar, numberProperties
                | "matrix" -> DiagramDataFamily.Matrix, heatmapProperties
                | "gantt" -> DiagramDataFamily.Gantt, ganttProperties
                | _ -> DiagramDataFamily.Chart, chartProperties

            let extraProps =
                bindingProps
                |> Seq.filter (fun name ->
                    not (baseProps |> List.exists (fun p -> String.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))))
                |> Seq.map (fun name ->
                    { PropertySchemas.Name = name
                      PropertySchemas.ValueType = PropertySchemas.PropertyValueType.Scalar })
                |> Seq.toList

            register
                { Id = id
                  DataFamily = family
                  Properties = baseProps @ extraProps
                  RequiredRoles = []
                  SupportsTopLimit = supportsTopLimit
                  AllowExtensionProperties = true }
        elif not (String.IsNullOrWhiteSpace id) then
            DashSpecKeywords.registerDiagramKind id

    let tryResolve kind = specs.TryGetValue kind

    let knownKinds () = specs.Keys |> Seq.sort |> Seq.toList

    /// <summary>Mandatory binding roles of a kind; unknown kinds carry none.</summary>
    let requiredRoles (kind: string) : string list =
        let found, spec = tryResolve kind
        if found then spec.RequiredRoles else []

    let getProperties kind = specs.[kind].Properties

    let allowExtensionProperties kind = specs.[kind].AllowExtensionProperties

    let supportsTopLimit kind = specs.[kind].SupportsTopLimit

    let allBindingProperties () =
        let merged = Dictionary<string, PropertySpec>(StringComparer.OrdinalIgnoreCase)
        for spec in specs.Values do
            for property in spec.Properties do
                merged.[property.Name] <- property
        merged.Values |> Seq.toList
