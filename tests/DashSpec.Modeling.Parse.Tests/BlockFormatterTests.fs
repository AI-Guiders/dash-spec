namespace DashSpec.Modeling.Parse.Tests

open Xunit
open DashSpec.Modeling.Parse.Formatting

module BlockFormatterTests =

    let private options =
        { DashSpecFormatOptions.defaultOptions with DashSpecBlankLineBetweenBlocks = true }

    [<Fact>]
    let ``manifest assignment is content not block opener`` () =
        let input =
            "@dashboard demo_soak\nruntime\nmanifest = \"demo.toml\"\nend runtime\nend dashboard\n"

        let formatted = DashSpecBlockFormatter.format input options
        let lines = formatted.Split('\n')
        Assert.Equal("        manifest = \"demo.toml\"", lines.[2])
        Assert.Equal("    end runtime", lines.[3])

    [<Fact>]
    let ``sibling dashboard blocks are separated by blank line after end`` () =
        let input =
            "@dashboard demo\nruntime\nx = 1\nend runtime\nconfiguration\ny = 2\nend configuration\nend dashboard\n"

        let formatted = DashSpecBlockFormatter.format input options
        let lines = formatted.Split('\n')
        Assert.Equal("    end runtime", lines.[3])
        Assert.Equal("", lines.[4])
        Assert.Equal("    configuration", lines.[5])

    [<Fact>]
    let ``module end aligns with module header`` () =
        let input =
            "@dashboard demo\nruntime\nx = 1\nend runtime\nend dashboard\n"

        let formatted = DashSpecBlockFormatter.format input options
        let lines = formatted.Split('\n')
        Assert.Equal("@dashboard demo", lines.[0])
        Assert.Equal("end dashboard", lines.[4])

    [<Fact>]
    let ``every end keyword aligns with its opener not body depth`` () =
        let input =
            "@dashboard demo\n        tab analytics as \"Analytics\"\n            cards\n                events_detail\n                  end cards\n        end tab\n    runtime\nx = 1\n      end runtime\n                  end dashboard\n"

        let formatted = DashSpecBlockFormatter.format input options
        let lines = formatted.Split('\n')

        Assert.Equal("@dashboard demo", lines.[0])
        Assert.Equal("    tab analytics as \"Analytics\"", lines.[1])
        Assert.Equal("        cards", lines.[2])
        Assert.Equal("            events_detail", lines.[3])
        Assert.Equal("        end cards", lines.[4])
        Assert.Equal("    end tab", lines.[5])
        Assert.Equal("", lines.[6])
        Assert.Equal("    runtime", lines.[7])
        Assert.Equal("        x = 1", lines.[8])
        Assert.Equal("    end runtime", lines.[9])
        Assert.Equal("end dashboard", lines.[10])
