namespace DashSpec.Modeling.Parse.Syntax

[<Struct>]
type TextSpan =
    { Start: int
      Length: int }

    member this.End = this.Start + this.Length

    static member Create start length = { Start = start; Length = max 0 length }

    member this.Contains offset = offset >= this.Start && offset < this.End
