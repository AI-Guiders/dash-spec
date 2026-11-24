namespace DashSpec.Modeling.Parse.Syntax

[<Struct>]
type TextSpan =
    { Start: int
      Length: int }

    member this.End = this.Start + this.Length

    static member Create start length = { Start = start; Length = max 0 length }

    member this.Contains offset = offset >= this.Start && offset < this.End

module TextSpan =

    /// 1-based line number of <paramref name="offset"/> in <paramref name="text"/>.
    let lineOfOffset (text: string) (offset: int) : int =
        if System.String.IsNullOrEmpty text || offset <= 0 then
            1
        else
            let clamped = min offset (text.Length - 1)
            let mutable line = 1
            for i = 0 to clamped do
                if text.[i] = '\n' then
                    line <- line + 1
            line
