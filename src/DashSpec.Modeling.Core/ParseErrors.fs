namespace DashSpec.Modeling.Core

open System

/// <summary>A dashflow contract violation surfaced past the syntax layer —
/// composite source composition, row-type resolution, or card materialization
/// (ADR-0126 §1.1). Distinct from DashSpecParseException: the flow text parsed,
/// the model itself does not compose.</summary>
type DashSpecFlowException(message: string) =
    inherit Exception(message)
