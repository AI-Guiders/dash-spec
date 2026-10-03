namespace DashSpec.Modeling.Parse.Tests

open System
open System.IO
open Xunit
open DashSpec.Modeling.Parse.Document

module DashflowConnectTests =

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
  use provider sqlserver
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

        
        infer
from view demo.v_daily_peak
      }
    }
  end report
"""

    [<Fact>]
    let ``connect flow resolves dashflow graph on tab module`` () =
        let dir = Path.Combine(Path.GetTempPath(), "dashflow-connect-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        File.WriteAllText(Path.Combine(dir, "types.dashtype"), typesText)
        File.WriteAllText(Path.Combine(dir, "peak.dashflow"), flowText)

        let specText =
            $"""
@tab flow_connect_test

connect
  flow "peak.dashflow"
end connect
{minimalTabReport}

end tab flow_connect_test
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
                match module'.Sources.[0].Provider with
                | DashflowProviderBinding.Named id -> Assert.Equal("sqlserver", id)
                | _ -> Assert.Fail("Expected named provider sqlserver")
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
    let ``connect flow and include dashflow are rejected`` () =
        let dir = Path.Combine(Path.GetTempPath(), "dashflow-dup-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        File.WriteAllText(Path.Combine(dir, "types.dashtype"), typesText)
        File.WriteAllText(Path.Combine(dir, "peak.dashflow"), flowText)

        let specText =
            $"""
@tab flow_dup_test

!include "peak.dashflow"
connect
  flow "peak.dashflow"
end connect
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

    [<Fact>]
    let ``wiring keyword is rejected`` () =
        let specText =
            """
@tab x
wiring
end wiring
report "T"
  card c as "C" {
    diagram table { columns x }
    datasource {       infer
from view v }

  }
end report
end tab x
"""
        Assert.Throws<DashSpec.Modeling.Core.DashSpecParseException>(fun () ->
            DocumentModuleParser.parseDocumentDefault specText None |> ignore)
        |> ignore
