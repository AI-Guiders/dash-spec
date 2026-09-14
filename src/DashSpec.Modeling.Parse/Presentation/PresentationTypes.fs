namespace DashSpec.Modeling.Parse.Presentation

open System.Collections.Generic

[<CLIMutable>]
type PresentationBlock =
    { UsePreset: string option
      Properties: IReadOnlyDictionary<string, string> }

[<CLIMutable>]
type PresentationInclude = { Kind: string; Reference: string }

[<CLIMutable>]
type PresentationModuleDocument =
    { Id: string
      Includes: IReadOnlyList<PresentationInclude>
      Local: PresentationBlock option }
