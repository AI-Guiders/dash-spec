namespace DashSpec.Modeling.Parse.Diagram

open System
open System.Collections.Generic
open DashSpec.Modeling.Parse

module DiagramKindRegistry =

    type PropertySpec = PropertySchemas.PropertySpec

    type DiagramKindSpec =
        { Id: string
          Properties: PropertySpec list
          SupportsTopLimit: bool
          AllowExtensionProperties: bool }

    let private chartProperties = PropertySchemas.chartDiagram

    let private tableProperties = PropertySchemas.tableDiagram

    let private numberProperties = PropertySchemas.numberDiagram

    let private heatmapProperties = PropertySchemas.heatmapDiagram

    let private specs =
        dict
            [ "line", { Id = "line"; Properties = chartProperties; SupportsTopLimit = false; AllowExtensionProperties = true }
              "area", { Id = "area"; Properties = chartProperties; SupportsTopLimit = false; AllowExtensionProperties = true }
              "sparkline", { Id = "sparkline"; Properties = chartProperties; SupportsTopLimit = false; AllowExtensionProperties = true }
              "bar", { Id = "bar"; Properties = chartProperties; SupportsTopLimit = true; AllowExtensionProperties = true }
              "pie", { Id = "pie"; Properties = chartProperties; SupportsTopLimit = true; AllowExtensionProperties = true }
              "donut", { Id = "donut"; Properties = chartProperties; SupportsTopLimit = true; AllowExtensionProperties = true }
              "doughnut", { Id = "doughnut"; Properties = chartProperties; SupportsTopLimit = true; AllowExtensionProperties = true }
              "scatter", { Id = "scatter"; Properties = chartProperties; SupportsTopLimit = false; AllowExtensionProperties = true }
              "histogram", { Id = "histogram"; Properties = chartProperties; SupportsTopLimit = false; AllowExtensionProperties = true }
              "box", { Id = "box"; Properties = chartProperties; SupportsTopLimit = false; AllowExtensionProperties = true }
              "boxplot", { Id = "boxplot"; Properties = chartProperties; SupportsTopLimit = false; AllowExtensionProperties = true }
              "treemap", { Id = "treemap"; Properties = chartProperties; SupportsTopLimit = true; AllowExtensionProperties = true }
              "gauge", { Id = "gauge"; Properties = chartProperties; SupportsTopLimit = false; AllowExtensionProperties = true }
              "windrose", { Id = "windrose"; Properties = chartProperties; SupportsTopLimit = true; AllowExtensionProperties = true }
              "wind_rose", { Id = "wind_rose"; Properties = chartProperties; SupportsTopLimit = true; AllowExtensionProperties = true }
              "table", { Id = "table"; Properties = tableProperties; SupportsTopLimit = true; AllowExtensionProperties = false }
              "number", { Id = "number"; Properties = numberProperties; SupportsTopLimit = false; AllowExtensionProperties = false }
              "heatmap", { Id = "heatmap"; Properties = heatmapProperties; SupportsTopLimit = false; AllowExtensionProperties = true } ]

    let tryResolve kind =
        specs.TryGetValue kind

    let getProperties kind = specs.[kind].Properties

    let allowExtensionProperties kind = specs.[kind].AllowExtensionProperties

    let allBindingProperties () =
        let merged = Dictionary<string, PropertySpec>(StringComparer.OrdinalIgnoreCase)
        for spec in specs.Values do
            for property in spec.Properties do
                merged.[property.Name] <- property
        merged.Values |> Seq.toList
