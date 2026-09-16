namespace DashSpec.Modeling.Parse.Tests

open Xunit
open DashSpec.Modeling.Parse.Formatting

module BlockFormatterTests =

    let private options =
        { DashSpecFormatOptions.defaultOptions with DashSpecPreserveBlankLineBeforeEnd = false }

    [<Fact>]
    let ``manifest assignment is content not block opener`` () =
        let input =
            "@dashboard demo_soak\nruntime\nmanifest = \"demo.toml\"\nend runtime\nend dashboard\n"

        let formatted = DashSpecBlockFormatter.format input options
        let lines = formatted.Split('\n')
        Assert.Equal("        manifest = \"demo.toml\"", lines.[2])
        Assert.Equal("    end runtime", lines.[3])
