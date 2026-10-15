namespace DashSpec.Modeling.Parse.Alias

open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

/// <summary>
/// <c>aliases … end aliases</c>: a table with columns <c>short | target</c>.
/// Short names are local to the module; each short name maps to exactly one target.
/// </summary>
module AliasBlockParser =

    /// Called after the <c>aliases</c> keyword has been consumed.
    let parse (reader: TokenReader) : Map<string, string> =
        let rows = ResizeArray<string>()

        while not (BlockSyntax.isBlockEnd reader "aliases" None) && not reader.IsEof do
            reader.SkipNewlines()

            if not (BlockSyntax.isBlockEnd reader "aliases" None) then
                if reader.CurrentKind <> TokenKind.TableRow then
                    raise (reader.Unexpected "table row '| short | target |'")

                rows.Add(reader.CurrentTableRow())
                reader.Advance()

        BlockSyntax.expectBlockEnd reader "aliases" None

        TableLiteral.parseRows (List.ofSeq rows)
        |> List.map (fun row ->
            match row.TryFind "short", row.TryFind "target" with
            | Some short, Some target when short <> "" && target <> "" -> short, target
            | _ -> raise (DashSpecParseException("aliases row requires 'short' and 'target' cells.")))
        |> fun pairs ->
            let duplicates = pairs |> List.countBy fst |> List.filter (fun (_, n) -> n > 1)
            match duplicates with
            | (short, _) :: _ -> raise (DashSpecParseException($"alias '{short}' is declared more than once."))
            | [] -> Map.ofList pairs
