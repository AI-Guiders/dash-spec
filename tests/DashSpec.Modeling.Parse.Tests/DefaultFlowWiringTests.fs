namespace DashSpec.Modeling.Parse.Tests

open System
open System.IO
open Xunit
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Document

[<Xunit.Collection("GridDefault")>]
module DefaultFlowWiringTests =

    /// ADR-0122: one source in the connected flow + one payload-less diagram slot
    /// + no explicit wiring of any kind => the default arc is synthesized into the
    /// same IR graph. The card below has NO data flow block at all.
    let private singleSourceFlow =
        """
module flow solo
    namespace Test.Flows

source solo_src {
  use provider sqlserver
  from view demo.v_heat
  ports
    default output utilization
    output stream utilization: UtilizationRow
  end ports
}

end flow
"""

    let private twoSourceFlow =
        """
module flow duo
    namespace Test.Flows

source first_src {
  use provider sqlserver
  from view demo.v_first
  ports
    default output rows
    output stream rows: UtilizationRow
  end ports
}

source second_src {
  use provider sqlserver
  from view demo.v_second
  ports
    default output rows
    output stream rows: UtilizationRow
  end ports
}

end flow
"""

    let private specWithoutFlowBlock (importLine: string) (reportBody: string) =
        $"""
module section default_wiring
  import diagrams from Test.Diags
  {importLine}

connect
  use flow the_flow
end connect

report "Default"
  type UtilizationRow
    string UserSam
    date usage_date
  end type
  defaults
    filter.usage_date.range = -7d..today
  end defaults
  filter usage_date
    bind date
      column = usage_date
    end bind
    show
      label = "Date"
    end show
  end filter
{reportBody}
end report

end section default_wiring
"""

    /// diagram ref only — no `data flow` anywhere near the card.
    let private cardWithoutFlow =
        """
  card solo as "Solo"
  diagram ref drill table
    columns = UserSam
  end table
  layout
    [ drill ]
  end layout
  end card
"""

    let private diagramUnit =
        """
module diagram solo_table
namespace Test.Diags

table
  columns = UserSam
end table
"""

    let private parseInTempDir (flowText: string) (specText: string) =
        let dir = Path.Combine(Path.GetTempPath(), "default-wiring-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        File.WriteAllText(Path.Combine(dir, "flow.ds"), flowText)
        File.WriteAllText(Path.Combine(dir, "diag.ds"), diagramUnit)
        try
            try
                DocumentModuleParser.parseDocumentDefault specText (Some dir) |> ignore
                "ok"
            with :? DashSpecParseException as ex ->
                ex.Message
        finally
            try
                Directory.Delete(dir, true)
            with _ ->
                ()

    let private parseDocumentInTempDir (flowText: string) (specText: string) =
        let dir = Path.Combine(Path.GetTempPath(), "default-wiring-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        File.WriteAllText(Path.Combine(dir, "flow.ds"), flowText)
        File.WriteAllText(Path.Combine(dir, "diag.ds"), diagramUnit)
        try
            DocumentModuleParser.parseDocumentDefault specText (Some dir)
        finally
            try
                Directory.Delete(dir, true)
            with _ ->
                ()

    [<Fact>]
    let ``card without any flow block synthesizes the default arc`` () =
        let spec = specWithoutFlowBlock "import flows from Test.Flows as the_flow" cardWithoutFlow
        let document = parseDocumentInTempDir singleSourceFlow spec

        let card = document.Cards.[0]
        Assert.True(card.DiagramSlots.Count = 1)
        let slot = (card.DiagramSlots |> Seq.exactlyOne).Value
        Assert.True(slot.FlowInput.IsSome, "default arc was not synthesized")
        Assert.Equal("solo_src", slot.FlowInput.Value.NodeId)
        Assert.Equal("utilization", slot.FlowInput.Value.PortName)
        Assert.True(card.FlowInput.IsSome, "primary card flow input must sync from the synthesized slot arc")

    [<Fact>]
    let ``several sources stay loud - no guessing`` () =
        let spec = specWithoutFlowBlock "import flows from Test.Flows as the_flow" cardWithoutFlow
        let outcome = parseInTempDir twoSourceFlow spec
        // two sources + no explicit wiring must NOT compile silently
        Assert.True(outcome <> "ok", "two sources compiled without explicit wiring")

    [<Fact>]
    let ``explicit report data flow wins over synthesis`` () =
        let explicitBody =
            """
  data flow
    first_src [rows] -> [rows] card.solo.drill
  end data flow

  card solo as "Solo"
  diagram ref drill table
    columns = UserSam
  end table
  layout
    [ drill ]
  end layout
  end card
"""
        let spec = specWithoutFlowBlock "import flows from Test.Flows as the_flow" explicitBody
        let document = parseDocumentInTempDir twoSourceFlow spec
        let slot = (document.Cards.[0].DiagramSlots |> Seq.exactlyOne).Value
        Assert.True(slot.FlowInput.IsSome)
        Assert.Equal("first_src", slot.FlowInput.Value.NodeId)

    [<Fact>]
    let ``card without wiring and without a connected flow stays loud`` () =
        let spec =
            specWithoutFlowBlock "" cardWithoutFlow
            |> fun text -> text.Replace("connect\n  use flow the_flow\nend connect", "")

        let outcome = parseInTempDir singleSourceFlow spec
        Assert.True(outcome <> "ok", "unwired card without a flow compiled silently")
