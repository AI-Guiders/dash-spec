namespace DashSpec.Modeling.Parse.Card

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

module CardVisibilityParser =

    let private isWhenBlockEnd (reader: TokenReader) (filterName: string option) =
        BlockSyntax.isBlockEnd reader "when" None
        || BlockSyntax.isBlockEnd reader "oversize" None
        || (filterName.IsSome && BlockSyntax.isBlockEnd reader filterName.Value None)

    let private expectWhenBlockEnd (reader: TokenReader) (filterName: string option) =
        if filterName.IsSome && BlockSyntax.isBlockEnd reader filterName.Value None then
            BlockSyntax.expectBlockEnd reader filterName.Value None
        elif BlockSyntax.isBlockEnd reader "oversize" None then
            BlockSyntax.expectBlockEnd reader "oversize" None
        else
            BlockSyntax.expectBlockEnd reader "when" None

    let private readMessageBody (reader: TokenReader) (cardId: string) (filterName: string) =
        reader.SkipNewlines()
        let mutable message: string option = None

        while not (isWhenBlockEnd reader (Some filterName)) && not reader.IsEof do
            reader.SkipNewlines()
            if isWhenBlockEnd reader (Some filterName) then ()
            else
                let key = reader.ReadIdent()
                if not (String.Equals(key, "message", StringComparison.OrdinalIgnoreCase)) then
                    raise (DashSpecParseException($"Card '{cardId}': when block supports only message = \"…\"."))
                reader.Expect TokenKind.Eq
                message <- Some(reader.ReadString())
                reader.SkipNewlines()

        expectWhenBlockEnd reader (Some filterName)

        match message with
        | None | Some (null | "") ->
            raise (DashSpecParseException($"Card '{cardId}': when message cannot be empty."))
        | Some value -> value

    let private tryReadMessageBody (reader: TokenReader) (cardId: string) (filterName: string) =
        if reader.IsEof || isWhenBlockEnd reader (Some filterName) then None
        else Some(readMessageBody reader cardId filterName)

    let parseFilterWhen (reader: TokenReader) (cardId: string) (filterName: string) =
        if String.IsNullOrWhiteSpace filterName then
            raise (DashSpecParseException($"Card '{cardId}': when requires filter name or oversize."))

        // ADR-0120: `when <filter>.no_data` — the param node's option domain is empty.
        if reader.IsAt TokenKind.Dot then
            reader.Advance()
            let state = reader.ReadIdent()

            if not (String.Equals(state, "no_data", StringComparison.OrdinalIgnoreCase)) then
                raise (DashSpecParseException($"Card '{cardId}': when '{filterName}' supports only .no_data, got '.{state}'."))

            reader.SkipNewlines()
            let message = tryReadMessageBody reader cardId filterName
            { FilterName = filterName; Mode = CardVisibilityMode.WhenNoData; Message = message }
        elif reader.TryKeyword "empty" then
            { FilterName = filterName; Mode = CardVisibilityMode.WhenEmpty; Message = None }
        elif reader.TryKeyword "set" then
            reader.SkipNewlines()
            { FilterName = filterName
              Mode = CardVisibilityMode.WhenSet
              Message = tryReadMessageBody reader cardId filterName }
        else
            reader.SkipNewlines()
            if not reader.IsEof && reader.TryKeyword "message" then
                reader.Expect TokenKind.Eq
                let inlineMessage = reader.ReadString()
                reader.SkipNewlines()
                expectWhenBlockEnd reader (Some filterName)
                { FilterName = filterName; Mode = CardVisibilityMode.WhenSet; Message = Some inlineMessage }
            elif not reader.IsEof && not (isWhenBlockEnd reader (Some filterName)) then
                { FilterName = filterName
                  Mode = CardVisibilityMode.WhenSet
                  Message = Some(readMessageBody reader cardId filterName) }
            else
                { FilterName = filterName; Mode = CardVisibilityMode.WhenSet; Message = None }

    let parseOversizeWhen (reader: TokenReader) (cardId: string) =
        reader.SkipNewlines()
        readMessageBody reader cardId "oversize"

    /// <summary>State words for the when construct (the message states; filter
    /// conditions keep their block form). Constants, not free text — the same
    /// word-registry discipline as click sources (ADR-0107).</summary>
    module WhenStates =
        /// <summary>The card's payload came back empty — the author's wording
        /// replaces the generic "no data for the period".</summary>
        let NoData = "no data"

        /// <summary>The matrix oversize protection tripped (when oversize block form).</summary>
        let Oversize = "oversize"

    /// <summary>
    /// The when construct's table surface (ADR-0106: one vocabulary, two surfaces, one
    /// semantics). Rows: <c>| state | message |</c>; the block forms (when oversize,
    /// when &lt;filter&gt;) stay valid and fill the same cells per the family rule.
    /// </summary>
    let whenDeclaration
        (cardId: string)
        (setOversize: string -> unit)
        (setNoData: string -> unit)
        : Construct.Declaration<string * string>
        =
        let cellsToRow (m: Map<string, string>) : string * string =
            let state =
                match Construct.tryCell m "state" with
                | Some s -> s.Trim().ToLowerInvariant()
                | None -> raise (DashSpecParseException($"Card '{cardId}': when table row requires a 'state' cell."))
            let message =
                match Construct.tryCell m "message" with
                | Some msg when msg.Length > 0 -> msg
                | _ -> raise (DashSpecParseException($"Card '{cardId}': when table row '{state}' requires a 'message' cell."))
            (state, message)

        let applyRow ((state, message): string * string) =
            match state with
            | s when String.Equals(s, WhenStates.Oversize, StringComparison.Ordinal) -> setOversize message
            | s when String.Equals(s, WhenStates.NoData, StringComparison.Ordinal) -> setNoData message
            | other ->
                let words = String.Join(" | ", [ WhenStates.NoData; WhenStates.Oversize ])
                raise (DashSpecParseException($"Card '{cardId}': unknown when state '{other}' ({words})."))

        { Name = "when"
          Vocabulary = [ "state"; "message" ]
          CellsToRow = cellsToRow
          ConstructRow = applyRow }

    let applyWhenForm
        (cardId: string)
        (setOversize: string -> unit)
        (setNoData: string -> unit)
        (form: Construct.ConstructForm)
        =
        Construct.apply (whenDeclaration cardId setOversize setNoData) form

    let applyWhenTableRows
        (cardId: string)
        (setOversize: string -> unit)
        (setNoData: string -> unit)
        (rows: string list)
        =
        Construct.applyTableRows (whenDeclaration cardId setOversize setNoData) rows
