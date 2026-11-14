namespace DashSpec.Modeling.Parse.Tests

open Xunit
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Document
open DashSpec.Modeling.Parse.Lexing

module ModuleHeaderParserTests =

    [<Fact>]
    let ``parses namespace and kind import with alias`` () =
        let reader =
            ParserUtilities.createReader
                """
namespace Demo.Analytics

import diagrams from Demo.Analytics.Diagrams as dia
import flows from Demo.Shared.Flows

runtime
"""

        let header = ModuleHeaderParser.parse reader
        Assert.Equal(Some "Demo.Analytics", header.Namespace)
        Assert.Equal(2, header.Imports.Count)
        Assert.Equal(ImportKind.Diagrams, header.Imports.[0].Kind)
        Assert.Equal("Demo.Analytics.Diagrams", header.Imports.[0].Namespace)
        Assert.Equal(Some "dia", header.Imports.[0].Alias)
        Assert.Equal(ImportKind.Flows, header.Imports.[1].Kind)
        Assert.Equal(None, header.Imports.[1].Alias)
        Assert.Equal("Flows", ModuleImportDirective.effectiveQualifier header.Imports.[1])
        Assert.True(reader.TryKeyword "runtime")

    [<Fact>]
    let ``stops before legacy import string`` () =
        let reader = ParserUtilities.createReader """import "../fragments/types.dashtypes" """
        let header = ModuleHeaderParser.parse reader
        Assert.Equal(0, header.Imports.Count)
        Assert.True(reader.TryKeyword "import")
        Assert.Equal(TokenKind.String, reader.CurrentKind)

    [<Fact>]
    let ``rejects duplicate namespace`` () =
        let reader = ParserUtilities.createReader "namespace A\nnamespace B\n"
        Assert.Throws<DashSpecParseException>(fun () -> ModuleHeaderParser.parse reader |> ignore)
        |> ignore

    [<Fact>]
    let ``parses from namespace import block with braces and property-style alias`` () =
        let reader =
            ParserUtilities.createReader
                """
namespace Demo.Stakeholder

from Demo.Stakeholder.Shared import {
    types
    layouts
    diagrams = stk
    flows = report_flow
}

from DashSpec.Std import presentations

runtime
"""

        let header = ModuleHeaderParser.parse reader
        Assert.Equal(Some "Demo.Stakeholder", header.Namespace)
        Assert.Equal(5, header.Imports.Count)

        Assert.Equal(ImportKind.Types, header.Imports.[0].Kind)
        Assert.Equal("Demo.Stakeholder.Shared", header.Imports.[0].Namespace)
        Assert.Equal(None, header.Imports.[0].Alias)

        Assert.Equal(ImportKind.Layouts, header.Imports.[1].Kind)
        Assert.Equal("Demo.Stakeholder.Shared", header.Imports.[1].Namespace)
        Assert.Equal(None, header.Imports.[1].Alias)

        Assert.Equal(ImportKind.Diagrams, header.Imports.[2].Kind)
        Assert.Equal("Demo.Stakeholder.Shared", header.Imports.[2].Namespace)
        Assert.Equal(Some "stk", header.Imports.[2].Alias)

        Assert.Equal(ImportKind.Flows, header.Imports.[3].Kind)
        Assert.Equal("Demo.Stakeholder.Shared", header.Imports.[3].Namespace)
        Assert.Equal(Some "report_flow", header.Imports.[3].Alias)

        Assert.Equal(ImportKind.Presentations, header.Imports.[4].Kind)
        Assert.Equal("DashSpec.Std", header.Imports.[4].Namespace)
        Assert.Equal(None, header.Imports.[4].Alias)
        Assert.True(reader.TryKeyword "runtime")

    [<Fact>]
    let ``surface syntax and UnitGrammar unwrap from namespace import block`` () =
        let src =
            """
module dashboard demo_soak
    namespace Demo.Soak
    from Demo.Shared import {
        types
        diagrams = stk
        flows = report_flow
    }
    report
        title = "Demo"
    end report
end dashboard
"""

        let tree = DashSpec.Modeling.Parse.Syntax.DashSpecSurfaceSyntax.parse src
        let env = UnitGrammar.unwrap DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind.Dashboard tree.Root
        Assert.Equal(Some "Demo.Soak", env.Namespace)
        Assert.Equal(3, env.Imports.Length)
        Assert.Equal(Some ImportKind.Types, env.Imports.[0].ImportKind)
        Assert.Equal("Demo.Shared", env.Imports.[0].Target)
        Assert.Equal(None, env.Imports.[0].Alias)
        Assert.Equal(Some ImportKind.Diagrams, env.Imports.[1].ImportKind)
        Assert.Equal(Some "stk", env.Imports.[1].Alias)
        Assert.Equal(Some ImportKind.Flows, env.Imports.[2].ImportKind)
        Assert.Equal(Some "report_flow", env.Imports.[2].Alias)

    [<Fact>]
    let ``whole module alias import stk = Namespace allows qualified stk.id access`` () =
        let dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "import-alias-" + System.Guid.NewGuid().ToString("N"))
        System.IO.Directory.CreateDirectory dir |> ignore

        try
            let presText =
                [ "module presentation tall_preset"
                  "namespace Demo.Stakeholder.Shared"
                  "  height = 420"
                  "  align = center"
                  "end presentation" ]
                |> String.concat "\n"

            let diagText =
                [ "module diagram activity_5min"
                  "namespace Demo.Stakeholder.Shared"
                  "  chrome"
                  "    use tall_preset"
                  "  end chrome"
                  "  heatmap"
                  "    x = d as \"D\""
                  "    y = a as \"A\""
                  "    value = v as \"V\""
                  "  end heatmap"
                  "end diagram" ]
                |> String.concat "\n"

            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "pres.ds"), presText)
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "diag.ds"), diagText)

            let spec =
                [ "module section stk_demo"
                  "  namespace Demo.Main"
                  "  import stk = Demo.Stakeholder.Shared"
                  ""
                  "  report \"Stakeholder\""
                  "    type R"
                  "      date d"
                  "      string a"
                  "      decimal v"
                  "    end type"
                  "    card c as \"C\""
                  "      data flow"
                  "        src [rows] -> [rows] __diagram__"
                  "      end data flow"
                  "      diagram stk.activity_5min"
                  "    end card"
                  "  end report"
                  "end section stk_demo" ]
                |> String.concat "\n"

            let doc = DocumentModuleParser.parseDocumentDefault spec (Some dir)
            let card = Assert.Single doc.Cards
            Assert.NotNull card.Diagram
            Assert.Equal("heatmap", card.Diagram.Kind)
            Assert.Equal("d", card.Diagram.Properties.["x"])
            Assert.True(doc.ModuleChartChromePresets.Value.ContainsKey("stk.tall_preset"))
            Assert.Equal("420", doc.ModuleChartChromePresets.Value.["stk.tall_preset"].Properties.["height"])
        finally
            if System.IO.Directory.Exists dir then
                System.IO.Directory.Delete(dir, true)
