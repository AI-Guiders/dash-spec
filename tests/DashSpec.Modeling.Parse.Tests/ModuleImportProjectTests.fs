namespace DashSpec.Modeling.Parse.Tests

open System
open System.IO
open Xunit
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Document
open DashSpec.Modeling.Parse.Project
open DashSpec.Modeling.Parse.Lexing
open System.Collections.Generic

module ModuleImportProjectTests =

    let private fixtureDir =
        Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "Fixtures", "import-project"))

    [<Fact>]
    let ``project index resolves import glob`` () =
        let cfg = DashSpecProjectConfig.load(Path.Combine(fixtureDir, "dashspec.toml"))
        let paths =
            DashSpecProjectIndex.collectPathsForImport cfg fixtureDir ImportKind.Diagrams "Demo.Charts.Diagrams" false

        Assert.Equal(1, paths.Length)
        Assert.EndsWith("demo_activity_5min_line.dashdiagram", paths.[0], StringComparison.OrdinalIgnoreCase)

    [<Fact>]
    let ``header import registers diagram units`` () =
        let text = File.ReadAllText(Path.Combine(fixtureDir, "tab-import.dashspec"))
        let reader = ParserUtilities.createReader text
        reader.SkipNewlines()
        reader.Expect Lexing.TokenKind.At |> ignore
        reader.ExpectKeyword "tab" |> ignore
        reader.ReadIdent() |> ignore
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let header = ModuleHeaderParser.parse reader
        let includes = DashSpec.Modeling.Parse.Include.ModuleIncludeState()
        let diagnostics = ResizeArray()

        ModuleImportResolver.applyHeaderImports
            header
            (Some fixtureDir)
            DocumentModuleKind.Tab
            includes
            DashSpecParseOptions.defaultOptions
            diagnostics

        Assert.True(includes.Diagrams.ContainsKey "demo_activity_5min_line")

    [<Fact>]
    let ``legacy include rejected when toml disables it`` () =
        let text =
            """
@tab x
!include "units/*.dashdiagram"
runtime
end runtime
report
  card c as "C"
    view
      diagram ref demo_activity_5min_line line
      end line
    end view
  end card
end report
end tab x
"""

        Assert.Throws<DashSpecParseException>(fun () ->
            DashSpecCompiler.compile text (Some fixtureDir) DashSpecParseOptions.defaultOptions
            |> ignore)
        |> ignore

    [<Fact>]
    let ``import in report body is rejected`` () =
        let text =
            """
@tab x
runtime
end runtime
report
  import diagrams from Demo.Charts.Diagrams
  card c as "C"
  end card
end report
end tab x
"""

        Assert.Throws<DashSpecParseException>(fun () ->
            DocumentModuleParser.parseDocumentDefault text (Some fixtureDir) |> ignore)
        |> ignore
