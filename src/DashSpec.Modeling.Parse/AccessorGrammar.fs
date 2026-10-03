namespace DashSpec.Modeling.Parse

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Lexing

/// Surface accessor chain: left-associative `.` (receiver.member).
/// Conceptually `. : Accessor -> string -> Accessor` — not a single lexer token.
/// SQL/view dotted names only — flow graph wires use link lines (`producer [out] -> [in] consumer`), not accessor chains.
module AccessorGrammar =

    [<RequireQualifiedAccess>]
    type Accessor =
        | Name of string
        | Select of target: Accessor * selector: string

        member this.SegmentCount =
            match this with
            | Name _ -> 1
            | Select (target, _) -> target.SegmentCount + 1

        member this.Dotted =
            match this with
            | Name name -> name
            | Select (target, selector) -> $"{target.Dotted}.{selector}"

    let private readFromName (reader: TokenReader) (root: string) =
        let rec extend (left: Accessor) =
            if reader.IsAt TokenKind.Dot then
                reader.Advance()
                extend (Accessor.Select(left, reader.ReadIdent()))
            else
                left

        extend (Accessor.Name root)

    /// `ident` ( `.` `ident` )* — stops before `..` (date range) because lexer emits `DotDot`.
    let read (reader: TokenReader) = readFromName reader (reader.ReadIdent())

    let readDotted (reader: TokenReader) = (read reader).Dotted

    let expectSegmentCount (accessor: Accessor) (count: int) (message: string) =
        if accessor.SegmentCount <> count then
            let detail = message.Replace("{name}", accessor.Dotted, StringComparison.Ordinal)
            raise (DashSpecParseException(detail))

    let readQualifiedName (reader: TokenReader) =
        reader.SkipNewlines()

        match reader.CurrentKind with
        | TokenKind.Raw -> reader.ReadRawBlock()
        | TokenKind.Ident -> readDotted reader
        | _ -> raise (reader.Unexpected "accessor")

    let readColumnBinding (reader: TokenReader) =
        let column = readQualifiedName reader

        if reader.TryKeyword "as" then
            { Column = column; Alias = Some(reader.ReadString()) }
        elif reader.RawKind = TokenKind.Ident && not (reader.IsOnNewline()) then
            let saved = reader.SavePosition()
            let alias = reader.ReadIdent()

            if reader.RawKind = TokenKind.Eq then
                reader.RestorePosition saved
                { Column = column; Alias = None }
            elif String.Equals(alias, "end", StringComparison.OrdinalIgnoreCase) then
                reader.RestorePosition saved
                { Column = column; Alias = None }
            else
                { Column = column; Alias = Some alias }
        else
            { Column = column; Alias = None }
