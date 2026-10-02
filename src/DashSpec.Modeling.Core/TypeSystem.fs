namespace DashSpec.Modeling.Core

/// <summary>Authoring SSOT for DashSpec primitives (ADR-0079 §3).</summary>
type DashPrimitive =
    | Bool
    | Int
    | Decimal
    | String
    | Duration
    | Date
    | Time
    | DateTime

[<CLIMutable>]
type RowFieldDef =
    { Name: string
      Kind: DashPrimitive
      Optional: bool }

[<CLIMutable>]
type RowTypeDef =
    { Name: string
      Fields: RowFieldDef[] }
