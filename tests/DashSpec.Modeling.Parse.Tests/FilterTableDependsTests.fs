namespace DashSpec.Modeling.Parse.Tests

open System
open System.IO
open Xunit
open DashSpec.Modeling.Parse.Document

[<Xunit.Collection("GridDefault")>]
module FilterTableDependsTests =

    /// ADR-0120 cascade domains: the filters-table `depends` column desugars into
    /// FilterDefinition.DependsOn; "-" stays unset. The referenced id may be a sibling
    /// or `report.<id>` — existence is validated downstream (options loader), not here.
    [<Fact>]
    let ``filters table depends column desugars into DependsOn`` () =
        let dir = Path.Combine(Path.GetTempPath(), "depends-flow-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        File.WriteAllText(Path.Combine(dir, "peak.ds"), CardInteriorFlowParseTests.flowTextForReuse)

        let specText =
            $"""
module section depends_flow
  import flows from Test.Flows as peak_flow

connect
  use flow peak_flow
end connect

report "Depends"
  type UtilizationRow
    string UserSam
  end type
  defaults
    filter.usage_date.range = -7d..today
  end defaults
  filters
    | id         | bind  | column     | label   | widget   | range      | depends           |
    | usage_date | date  | usage_date | Период  | range    | -7d..today | -                 |
    | app_name   | field | dbo.t.app  | Продукт | combobox | -          | usage_date        |
    | host       | field | dbo.t.h    | Хост    | combobox | -          | report.usage_date |
  end filters

  data flow
    utilization [utilization] -> [rows] card.peak.heatmap
  end data flow

  card peak as "Peak"
  diagram ref heatmap table
    columns = UserSam
  end table
  end card
end report

end section depends_flow
"""

        try
            let document = DocumentModuleParser.parseDocumentDefault specText (Some dir)
            let byName (name: string) =
                document.Filters
                |> Seq.find (fun f -> String.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))

            Assert.True((byName "usage_date").DependsOn.IsNone)
            Assert.Equal(Some "usage_date", (byName "app_name").DependsOn)
            Assert.Equal(Some "report.usage_date", (byName "host").DependsOn)
        finally
            try
                Directory.Delete(dir, true)
            with _ ->
                ()
