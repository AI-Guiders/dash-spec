namespace DashSpec.Modeling.Parse

open System

/// <summary>Table literal rows: <c>| a | b |</c>. First row is the header; separator rows (<c>|---|---|</c>) are skipped.</summary>
module TableLiteral =

    let splitCells (row: string) : string[] =
        let trimmed = row.Trim()
        let inner = if trimmed.StartsWith "|" then trimmed.Substring 1 else trimmed
        let body = if inner.EndsWith "|" then inner.Substring(0, inner.Length - 1) else inner
        body.Split('|') |> Array.map (fun c -> c.Trim())

    let isSeparator (cells: string[]) =
        cells.Length > 0
        && cells |> Array.forall (fun c -> c.Length > 0 && c.Replace("-", "").Length = 0)

    /// <summary>Rows → one map per data row, keyed by lower-cased header cell.</summary>
    let parseRows (rows: string list) : Map<string, string> list =
        match rows |> List.map splitCells with
        | header :: rest ->
            let keys = header |> Array.map (fun h -> h.ToLowerInvariant())

            rest
            |> List.filter (fun cells -> not (isSeparator cells))
            |> List.map (fun cells ->
                [ for i in 0 .. (min (cells.Length - 1) (keys.Length - 1)) do
                    if keys.[i].Length > 0 then yield keys.[i], cells.[i] ]
                |> Map.ofList)
        | [] -> []
