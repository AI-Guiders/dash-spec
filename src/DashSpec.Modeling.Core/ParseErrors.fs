namespace DashSpec.Modeling.Core

open System

/// <summary>Parse failure with optional source offset (F# SSOT per ADR-0048).</summary>
type DashSpecParseException(message: string, ?sourceOffset: int) =
    inherit Exception(message)

    /// <summary>0-based character offset in source text, when known.</summary>
    member _.SourceOffset = sourceOffset |> Option.toNullable

/// <summary>A dashflow contract violation surfaced past the syntax layer —
/// composite source composition, row-type resolution, or card materialization
/// (ADR-0126 §1.1). Distinct from DashSpecParseException: the flow text parsed,
/// the model itself does not compose.</summary>
type DashSpecFlowException(message: string) =
    inherit Exception(message)
