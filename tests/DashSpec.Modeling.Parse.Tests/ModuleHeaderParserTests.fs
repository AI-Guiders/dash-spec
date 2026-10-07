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
namespace Demo.Stakeholder

import diagrams from Demo.Stakeholder.Diagrams as stk
import flows from Demo.Report

runtime
"""

        let header = ModuleHeaderParser.parse reader
        Assert.Equal(Some "Demo.Stakeholder", header.Namespace)
        Assert.Equal(2, header.Imports.Count)
        Assert.Equal(ImportKind.Diagrams, header.Imports.[0].Kind)
        Assert.Equal("Demo.Stakeholder.Diagrams", header.Imports.[0].Namespace)
        Assert.Equal(Some "stk", header.Imports.[0].Alias)
        Assert.Equal(ImportKind.Flows, header.Imports.[1].Kind)
        Assert.Equal(None, header.Imports.[1].Alias)
        Assert.Equal("Report", ModuleImportDirective.effectiveQualifier header.Imports.[1])
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
