namespace DashSpec.Modeling.Parse.Tooltip

open System.Collections.Generic

[<CLIMutable>]
type TooltipDefinition =
    { Id: string
      Variables: IReadOnlyDictionary<string, string>
      Template: string }