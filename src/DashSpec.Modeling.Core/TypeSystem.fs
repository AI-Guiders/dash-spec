namespace DashSpec.Modeling.Core

/// <summary>Canonical DashSpec primitives (ADR-0079 §3).</summary>
type DashPrimitive =
    | Bool
    | Int
    | Decimal
    | String
    | Duration
    | Date
    | Time
    | DateTime

/// <summary>Value types on fields and ports — no references, no unbounded arrays (ADR-0079).</summary>
[<RequireQualifiedAccess>]
type DashType =
    | Primitive of DashPrimitive
    | Named of typeName: string
    | FixedArray of element: DashPrimitive * length: int

/// <summary>Data-flow port types — <c>stream</c> / <c>scalar</c> surface (ADR-0078).</summary>
[<RequireQualifiedAccess>]
type DashPortType =
    | Stream of rowTypeName: string
    | Scalar of valueTypeName: string

    /// <summary>Legacy IR alias.</summary>
    static member Rows name = DashPortType.Stream name

[<CLIMutable>]
type RowFieldMetadata =
    { Format: string option
      Label: string option }

module private RowFieldMetadataTable =
    let table = System.Runtime.CompilerServices.ConditionalWeakTable<obj, RowFieldMetadata>()

[<CLIMutable>]
type RowFieldDef =
    { Name: string
      Type: DashType
      Optional: bool }

    member this.Format : string option =
        match RowFieldMetadataTable.table.TryGetValue(box this) with
        | true, m -> m.Format
        | false, _ -> None

    member this.Label : string option =
        match RowFieldMetadataTable.table.TryGetValue(box this) with
        | true, m -> m.Label
        | false, _ -> None

module RowFieldDef =
    let withMetadata (format: string option) (label: string option) (field: RowFieldDef) : RowFieldDef =
        if format.IsSome || label.IsSome then
            RowFieldMetadataTable.table.AddOrUpdate(box field, { Format = format; Label = label })
        field

/// <summary>UDT / aggregate <c>type … end type</c> (ADR-0079).</summary>
[<CLIMutable>]
type RowTypeDef =
    { Name: string
      Fields: RowFieldDef[] }

module DashPrimitive =
    /// <summary>Case-sensitive primitive keywords on field lines (ADR-0079).</summary>
    let tryParseFieldKeyword (name: string) =
        match name with
        | "bool" -> Some DashPrimitive.Bool
        | "int" -> Some DashPrimitive.Int
        | "decimal" -> Some DashPrimitive.Decimal
        | "string" -> Some DashPrimitive.String
        | "duration" -> Some DashPrimitive.Duration
        | "date" -> Some DashPrimitive.Date
        | "time" -> Some DashPrimitive.Time
        | "datetime" -> Some DashPrimitive.DateTime
        | _ -> None

    let tryParse (name: string) =
        tryParseFieldKeyword (name.ToLowerInvariant())

    let toString (primitive: DashPrimitive) =
        match primitive with
        | DashPrimitive.Bool -> "bool"
        | DashPrimitive.Int -> "int"
        | DashPrimitive.Decimal -> "decimal"
        | DashPrimitive.String -> "string"
        | DashPrimitive.Duration -> "duration"
        | DashPrimitive.Date -> "date"
        | DashPrimitive.Time -> "time"
        | DashPrimitive.DateTime -> "datetime"

module DashType =
    let primitive kind = DashType.Primitive kind

    let isRows (_: DashType) = false

    let describe (dashType: DashType) =
        match dashType with
        | DashType.Primitive p -> DashPrimitive.toString p
        | DashType.Named name -> name
        | DashType.FixedArray(element, length) -> $"array {DashPrimitive.toString element} {length}"
