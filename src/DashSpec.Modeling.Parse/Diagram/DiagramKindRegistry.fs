namespace DashSpec.Modeling.Parse.Diagram

open System
open System.Collections.Generic
open DashSpec.Modeling.Parse

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
          SupportsTopLimit: bool
          AllowExtensionProperties: bool }

    let private chartProperties = PropertySchemas.chartDiagram

    let private tableProperties = PropertySchemas.tableDiagram

    let private numberProperties = PropertySchemas.numberDiagram

    let private heatmapProperties = PropertySchemas.heatmapDiagram

    let private ganttProperties = PropertySchemas.ganttDiagram

    let private specs =
        dict
            [ "line", { Id = "line"; DataFamily = DiagramDataFamily.Chart; Properties = chartProperties; SupportsTopLimit = false; AllowExtensionProperties = true }
              "area", { Id = "area"; DataFamily = DiagramDataFamily.Chart; Properties = chartProperties; SupportsTopLimit = false; AllowExtensionProperties = true }
              "sparkline", { Id = "sparkline"; DataFamily = DiagramDataFamily.Chart; Properties = chartProperties; SupportsTopLimit = false; AllowExtensionProperties = true }
              "bar", { Id = "bar"; DataFamily = DiagramDataFamily.Chart; Properties = chartProperties; SupportsTopLimit = true; AllowExtensionProperties = true }
              "pie", { Id = "pie"; DataFamily = DiagramDataFamily.Chart; Properties = chartProperties; SupportsTopLimit = true; AllowExtensionProperties = true }
              "donut", { Id = "donut"; DataFamily = DiagramDataFamily.Chart; Properties = chartProperties; SupportsTopLimit = true; AllowExtensionProperties = true }
              "doughnut", { Id = "doughnut"; DataFamily = DiagramDataFamily.Chart; Properties = chartProperties; SupportsTopLimit = true; AllowExtensionProperties = true }
              "scatter", { Id = "scatter"; DataFamily = DiagramDataFamily.Chart; Properties = chartProperties; SupportsTopLimit = false; AllowExtensionProperties = true }
              "histogram", { Id = "histogram"; DataFamily = DiagramDataFamily.Chart; Properties = chartProperties; SupportsTopLimit = false; AllowExtensionProperties = true }
              "box", { Id = "box"; DataFamily = DiagramDataFamily.Chart; Properties = chartProperties; SupportsTopLimit = false; AllowExtensionProperties = true }
              "boxplot", { Id = "boxplot"; DataFamily = DiagramDataFamily.Chart; Properties = chartProperties; SupportsTopLimit = false; AllowExtensionProperties = true }
              "treemap", { Id = "treemap"; DataFamily = DiagramDataFamily.Chart; Properties = chartProperties; SupportsTopLimit = true; AllowExtensionProperties = true }
              "gauge", { Id = "gauge"; DataFamily = DiagramDataFamily.Chart; Properties = chartProperties; SupportsTopLimit = false; AllowExtensionProperties = true }
              "windrose", { Id = "windrose"; DataFamily = DiagramDataFamily.Chart; Properties = chartProperties; SupportsTopLimit = true; AllowExtensionProperties = true }
              "wind_rose", { Id = "wind_rose"; DataFamily = DiagramDataFamily.Chart; Properties = chartProperties; SupportsTopLimit = true; AllowExtensionProperties = true }
              "table", { Id = "table"; DataFamily = DiagramDataFamily.Table; Properties = tableProperties; SupportsTopLimit = true; AllowExtensionProperties = false }
              "number", { Id = "number"; DataFamily = DiagramDataFamily.Scalar; Properties = numberProperties; SupportsTopLimit = false; AllowExtensionProperties = false }
              "heatmap", { Id = "heatmap"; DataFamily = DiagramDataFamily.Matrix; Properties = heatmapProperties; SupportsTopLimit = false; AllowExtensionProperties = true }
              "gantt", { Id = "gantt"; DataFamily = DiagramDataFamily.Gantt; Properties = ganttProperties; SupportsTopLimit = true; AllowExtensionProperties = true } ]

    let tryResolve kind = specs.TryGetValue kind

    let knownKinds () = specs.Keys |> Seq.sort |> Seq.toList

    let getProperties kind = specs.[kind].Properties

    let allowExtensionProperties kind = specs.[kind].AllowExtensionProperties

    let supportsTopLimit kind = specs.[kind].SupportsTopLimit

    let allBindingProperties () =
        let merged = Dictionary<string, PropertySpec>(StringComparer.OrdinalIgnoreCase)
        for spec in specs.Values do
            for property in spec.Properties do
                merged.[property.Name] <- property
        merged.Values |> Seq.toList
