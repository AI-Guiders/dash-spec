namespace DashSpec.Modeling.Parse.Tests

open System
open Xunit
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Core
open DashSpec.Modeling.Core.TransformDeclarations
open DashSpec.Modeling.Parse.Types
open DashSpec.Modeling.Parse.DataFlow

/// ADR-0127: transform port type inference. Sources are the explicit typing
/// boundary; transformer ports are inferred from the incoming edge and the
/// step's declared type-effect; explicit output annotations are verified
/// contracts (DFLOW010), not switches.
module TypeInferenceTests =

    let private typesText =
        """
module types inference_types

type EventRow
  date usage_date
  string app_name
  int sessions
end type
"""

    let private parseFlow (flowText: string) =
        let catalog = TypeCatalog.ofDefinitions (TypeModuleParser.parseTypesModule typesText)
        DashflowModuleParser.parseModule flowText catalog

    let private flowWithTransformer (transformerBody: string) (link: string) =
        $"""
module flow inference_flow

source events {{
  use provider infer
  from view demo.v_events
  ports
    default output rows
    output stream rows: EventRow
  end ports
}}

{transformerBody}

{link}

end flow
"""

    [<Fact>]
    let ``ports-less summarize synthesizes BaseSummaryRow with by and value`` () =
        let module' =
            parseFlow (
                flowWithTransformer
                    """
transformer by_app {
  transform use summarize { by = app_name
    agg = count
  }
}
"""
                    "events [rows] -> [raw] by_app"
            )

        let synthesized = module'.InferredRowTypes
        Assert.Equal(1, synthesized.Length)

        let def = synthesized[0]
        Assert.Equal("EventSummaryRow", def.Name)
        Assert.Equal(2, def.Fields.Length)
        Assert.Equal("app_name", def.Fields[0].Name)
        Assert.Equal("value", def.Fields[1].Name)
        Assert.Equal(DashPrimitive.String, (match def.Fields[0].Type with DashType.Primitive p -> p | _ -> failwith "primitive expected"))
        Assert.Equal(DashPrimitive.Int, (match def.Fields[1].Type with DashType.Primitive p -> p | _ -> failwith "primitive expected"))

        // the untyped output port got the inferred name
        let byApp = module'.Transformers |> Array.find (fun t -> t.Id = "by_app")
        let (port, rowType) = byApp.Outputs[0]
        Assert.Equal("rows", port)
        Assert.Equal("EventSummaryRow", rowType)

        // and the graph typechecks clean against the extended catalog
        Assert.Empty(module'.Diagnostics)

    [<Fact>]
    let ``sum of a decimal column keeps the value column's primitive`` () =
        // sessions is int → sum(usage) over `sessions` gives int value
        let module' =
            parseFlow (
                flowWithTransformer
                    """
transformer total_sessions {
  transform use summarize { by = app_name
    agg = sum
    value = sessions
  }
}
"""
                    "events [rows] -> [raw] total_sessions"
            )

        let def = module'.InferredRowTypes[0]
        let valueField = def.Fields |> Array.find (fun f -> f.Name = "value")
        Assert.Equal(DashPrimitive.Int, (match valueField.Type with DashType.Primitive p -> p | _ -> failwith "primitive expected"))

    [<Fact>]
    let ``identity chain synthesizes nothing and passes the source type through`` () =
        let module' =
            parseFlow (
                flowWithTransformer
                    """
transformer localized {
  transform use to_zone { zone = UTC+3 }
}
"""
                    "events [rows] -> [raw] localized"
            )

        Assert.Empty(module'.InferredRowTypes)
        let localized = module'.Transformers |> Array.find (fun t -> t.Id = "localized")
        let (_, rowType) = localized.Outputs[0]
        Assert.Equal("EventRow", rowType)
        Assert.Empty(module'.Diagnostics)

    [<Fact>]
    let ``declared output matching the inferred type is kept with no diagnostics`` () =
        // a hand-written structural twin: the compiler must reuse it, not synthesize
        let typesWithTwin =
            typesText
            + """
type AppCountRow
  string app_name
  int value
end type
"""

        let catalog = TypeCatalog.ofDefinitions (TypeModuleParser.parseTypesModule typesWithTwin)

        let flowText =
            """
module flow inference_flow

source events {
  use provider infer
  from view demo.v_events
  ports
    default output rows
    output stream rows: EventRow
  end ports
}

transformer by_app {
  transform use summarize { by = app_name
    agg = count
  }
}

events [rows] -> [raw] by_app

end flow
"""

        let module' = DashflowModuleParser.parseModule flowText catalog
        Assert.Empty(module'.InferredRowTypes) // the declared twin was reused
        let byApp = module'.Transformers |> Array.find (fun t -> t.Id = "by_app")
        Assert.Equal("AppCountRow", snd byApp.Outputs[0])

    [<Fact>]
    let ``declared output that contradicts the inferred type reports DFLOW010`` () =
        let module' =
            parseFlow (
                $"""
module flow inference_flow

source events {{
  use provider infer
  from view demo.v_events
  ports
    default output rows
    output stream rows: EventRow
  end ports
}}

transformer by_app {{
  ports
    input stream raw: EventRow
    output stream rows: EventRow
  end ports
  transform use summarize {{ by = app_name
    agg = count
  }}
}}

events [rows] -> [raw] by_app

end flow
"""
            )

        Assert.Contains(module'.Diagnostics, fun d -> d.Code = "DFLOW010")

    [<Fact>]
    let ``summarize by an unknown column reports DFLOW012`` () =
        let module' =
            parseFlow (
                flowWithTransformer
                    """
transformer by_missing {
  transform use summarize { by = no_such_column
    agg = count
  }
}
"""
                    "events [rows] -> [raw] by_missing"
            )

        Assert.Contains(module'.Diagnostics, fun d -> d.Code = "DFLOW012")

    [<Fact>]
    let ``default ports make a ports-less transformer linkable`` () =
        // ADR-0127 §4: unary defaults raw/rows — the link uses [raw]/[rows] without
        // any ports block on the transformer.
        let module' =
            parseFlow (
                flowWithTransformer
                    """
transformer passthrough {
  transform use sort { by = app_name }
}
"""
                    "events [rows] -> [raw] passthrough"
            )

        let passthrough = module'.Transformers |> Array.find (fun t -> t.Id = "passthrough")
        Assert.Equal("raw", passthrough.Inputs[0].Name)
        Assert.Equal("rows", fst passthrough.Outputs[0])
        Assert.Equal("EventRow", snd passthrough.Outputs[0])
        Assert.Empty(module'.Diagnostics)

    [<Fact>]
    let ``declaration units carry the type_effect`` () =
        // the stdlib registration is lazy; ensure it and probe two declarations
        TransformDeclarationParser.ensureStdlibRegistered ()

        Assert.Equal(TransformTypeEffect.Projection, TransformDeclarations.effectOf "summarize")
        Assert.Equal(TransformTypeEffect.Identity, TransformDeclarations.effectOf "to_zone")

        // undeclared ids are opaque — inference never runs for them
        Assert.Equal(TransformTypeEffect.Opaque, TransformDeclarations.effectOf "no_such_transform")
