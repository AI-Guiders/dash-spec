namespace DashSpec.Modeling.Parse.Tests

open Xunit
open DashSpec.Modeling.Parse.Types

module TypeModuleParserTests =

    [<Fact>]
    let ``parse type block with optional fields`` () =
        let text =
            """
type StakeholderKpiRow
  string Name
  decimal Value
end type
"""

        let defs = TypeModuleParser.parseTypesModule text
        Assert.Equal(1, defs.Length)
        Assert.Equal("StakeholderKpiRow", defs.[0].Name)
        Assert.Equal(2, defs.[0].Fields.Length)
