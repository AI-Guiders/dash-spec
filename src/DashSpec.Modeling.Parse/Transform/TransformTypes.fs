namespace DashSpec.Modeling.Parse.Transform

[<CLIMutable>]
type SeriesTransformBlock =
    { UsePreset: string option
      Max: int option
      OtherLabel: string option }
