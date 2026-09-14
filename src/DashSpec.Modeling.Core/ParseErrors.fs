namespace DashSpec.Modeling.Core

open System

/// <summary>Parse failure with optional source offset (F# SSOT per ADR-0048).</summary>
type DashSpecParseException(message: string, ?sourceOffset: int) =
    inherit Exception(message)

    /// <summary>0-based character offset in source text, when known.</summary>
    member _.SourceOffset = sourceOffset |> Option.toNullable
