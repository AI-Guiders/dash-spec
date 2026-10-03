namespace DashSpec.Modeling.Parse.Tests

open System
open System.IO
open Xunit
open DashSpec.Modeling.Parse.Document

module DashflowWiringTests =

    let private typesText =
        """
type UtilizationRow
  string UserSam
  int ConcurrentApps
end type
"""

    let private flowText =
        """
@flow peak

source utilization {
  from view demo.v_daily_peak
  ports
    default output utilization
    output stream utilization: UtilizationRow
  end ports
}

end flow
"""

    let private minimalTabReport =
        """
  report "Flow test"
    type UtilizationRow
      string UserSam
      int ConcurrentApps
    end type
    card c as "C" {
      diagram table {
        columns UserSam
      }
      datasource {
        from view demo.v_daily_peak
      }
    }
  end report
"""

    [<Fact>]
    let ``wiring flow resolves dashflow graph on tab module`` () =
        let dir = Path.Combine(Path.GetTempPath(), "dashflow-wiring-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        File.WriteAllText(Path.Combine(dir, "types.dashtype"), typesText)
        File.WriteAllText(Path.Combine(dir, "peak.dashflow"), flowText)

        let specText =
            $"""
@tab flow_wiring_test

wiring
  flow "peak.dashflow"
end wiring
{minimalTabReport}

end tab flow_wiring_test
"""

        try
            let document = DocumentModuleParser.parseDocumentDefault specText (Some dir)
            Assert.True(document.Dashflow.IsSome)

            match document.Dashflow with
            | None -> Assert.Fail("Expected resolved dashflow")
            | Some module' ->
                Assert.Equal("peak", module'.FlowId)
                Assert.Empty(module'.Diagnostics)
                Assert.Equal(Some "peak.dashflow", document.DashflowPath)
        finally
            try
                Directory.Delete(dir, true)
            with _ ->
                ()

    [<Fact>]
    let ``include dashflow registers graph`` () =
        let dir = Path.Combine(Path.GetTempPath(), "dashflow-include-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        File.WriteAllText(Path.Combine(dir, "types.dashtype"), typesText)
        File.WriteAllText(Path.Combine(dir, "peak.dashflow"), flowText)

        let specText =
            $"""
@tab flow_include_test

!include "types.dashtype"
!include "peak.dashflow"
{minimalTabReport}

end tab flow_include_test
"""

        try
            let document = DocumentModuleParser.parseDocumentDefault specText (Some dir)
            Assert.True(document.Dashflow.IsSome)
            Assert.Equal("peak", document.Dashflow.Value.FlowId)
            Assert.True(document.DashflowPath.IsNone)
        finally
            try
                Directory.Delete(dir, true)
            with _ ->
                ()

    [<Fact>]
    let ``wiring flow and include dashflow are rejected`` () =
        let dir = Path.Combine(Path.GetTempPath(), "dashflow-dup-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        File.WriteAllText(Path.Combine(dir, "types.dashtype"), typesText)
        File.WriteAllText(Path.Combine(dir, "peak.dashflow"), flowText)

        let specText =
            $"""
@tab flow_dup_test

!include "peak.dashflow"
wiring
  flow "peak.dashflow"
end wiring
{minimalTabReport}

end tab flow_dup_test
"""

        try
            Assert.Throws<DashSpec.Modeling.Core.DashSpecParseException>(fun () ->
                DocumentModuleParser.parseDocumentDefault specText (Some dir) |> ignore)
            |> ignore
        finally
            try
                Directory.Delete(dir, true)
            with _ ->
                ()
