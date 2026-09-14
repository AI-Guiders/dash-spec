namespace DashSpec.Modeling.Parse.Palette

open System.Collections.Generic

[<CLIMutable>]
type PaletteDocument =
    { Id: string
      Properties: IReadOnlyDictionary<string, string> }
