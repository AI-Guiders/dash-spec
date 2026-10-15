namespace DashSpec.Modeling.Parse.Presentation

open System.Collections.Generic

[<CLIMutable>]
type PresentationBlock =
    { UsePreset: string option
      Properties: IReadOnlyDictionary<string, string> }

[<CLIMutable>]
type PresentationModuleDocument =
    { Id: string
      Local: PresentationBlock option }
