namespace DashSpec.Modeling.Parse.Tests

open Xunit
open DashSpec.Modeling.Parse.Formatting

module BlockFormatterTests =

    let private options =
        { DashSpecFormatOptions.defaultOptions with
            DashSpecPreserveBlankLineBeforeEnd = true
            DashSpecBlankLineBetweenBlocks = true }

    [<Fact>]
    let ``manifest assignment is content not block opener`` () =
        let input =
            "@dashboard demo_soak\nruntime\nmanifest = \"demo.toml\"\nend runtime\nend dashboard\n"

        let formatted = DashSpecBlockFormatter.format input options
        let lines = formatted.Split('\n')
        Assert.Equal("        manifest = \"demo.toml\"", lines.[2])
        Assert.Equal("    end runtime", lines.[4])

    [<Fact>]
    let ``sibling dashboard blocks are separated by blank line after end`` () =
        let input =
            "@dashboard demo\nruntime\nx = 1\nend runtime\nconfiguration\ny = 2\nend configuration\nend dashboard\n"

        let formatted = DashSpecBlockFormatter.format input options
        let lines = formatted.Split('\n')
        Assert.Equal("    end runtime", lines.[4])
        Assert.Equal("", lines.[5])
        Assert.Equal("    configuration", lines.[6])
