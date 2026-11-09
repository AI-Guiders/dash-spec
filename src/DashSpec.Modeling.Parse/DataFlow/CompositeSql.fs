namespace DashSpec.Modeling.Parse.DataFlow

open System
open DashSpec.Modeling.Core

/// <summary>ADR-0126 slice 3: SQL composition of composite sources
/// (<c>InnerJoin/LeftJoin/RightJoin/FullJoin/UnionAll</c>) — the single source
/// of truth consumed by both the card datasource materializer (Parse,
/// <c>DocumentFlowMaterializer</c>) and the managed-view emitter (Compilation,
/// <c>ManagedViewsEmitter</c>). Composable constituents are view / sql-query
/// leaves — SqlFile/Xlsx cannot be composed server-side and fail loudly. Join
/// sides carry the aliases <c>l</c>/<c>r</c> (the ADR: filter ownership is
/// decided by the schema, not by branch swap); union wraps into a derived
/// table <c>u</c> so the outer query stays unqualified and unambiguous.</summary>
module CompositeSql =

    /// <summary>SQL material of a leaf: a bare view name, or a parenthesized
    /// sql-query body. SqlFile/Xlsx have no server-side SQL — None.</summary>
    let private leafFrom (kind: SourceFromKind) (value: string) =
        match kind with
        | SourceFromKind.View -> Some value
        | SourceFromKind.SqlQuery -> Some $"(\n{value}\n)"
        | _ -> None

    /// <summary>The FROM-body of a composite side: the constituent must exist and
    /// be a composable leaf (ADR-0126 §1).</summary>
    let private sideOf (source: DashflowSourceDef) (module': DashflowModule) (sideName: string) (constituentId: string) =
        let constituent =
            module'.Sources
            |> Array.tryFind (fun s -> String.Equals(s.Id, constituentId, StringComparison.OrdinalIgnoreCase))

        match constituent with
        | None ->
            raise (
                DashSpecFlowException(
                    $"composite source '{source.Id}': {sideName} side references unknown or non-leaf source '{constituentId}' — compose view/sql-query leaves only."))
        | Some def ->
            match def.Origin with
            | SourceOrigin.Leaf(kind, value, _) ->
                match leafFrom kind value with
                | Some from -> from
                | None ->
                    raise (
                        DashSpecFlowException(
                            $"composite source '{source.Id}': {sideName} side '{constituentId}' is a sql-file/xlsx leaf — only view and sql-query leaves compose server-side."))
            | _ ->
                raise (
                    DashSpecFlowException(
                        $"composite source '{source.Id}': {sideName} side references unknown or non-leaf source '{constituentId}' — compose view/sql-query leaves only."))

    let private sidesOf (source: DashflowSourceDef) (module': DashflowModule) =
        match source.Origin with
        | SourceOrigin.InnerJoin(left, right, _) -> (sideOf source module' "left" left, sideOf source module' "right" right)
        | SourceOrigin.LeftJoin(left, right, _) -> (sideOf source module' "left" left, sideOf source module' "right" right)
        | SourceOrigin.RightJoin(left, right, _) -> (sideOf source module' "left" left, sideOf source module' "right" right)
        | SourceOrigin.FullJoin(left, right, _) -> (sideOf source module' "left" left, sideOf source module' "right" right)
        | SourceOrigin.UnionAll(left, right) -> (sideOf source module' "left" left, sideOf source module' "right" right)
        | SourceOrigin.Leaf _ -> ("", "")

    let private conditionOf (source: DashflowSourceDef) (cond: JoinCondition) =
        if not cond.On.IsEmpty then
            cond.On |> List.map (fun key -> $"l.{key} = r.{key}") |> String.concat "\n  AND "
        elif not cond.LeftKey.IsEmpty && not cond.RightKey.IsEmpty then
            if cond.LeftKey.Length <> cond.RightKey.Length then
                raise (
                    DashSpecFlowException(
                        $"composite source '{source.Id}': left_key ({cond.LeftKey.Length}) and right_key ({cond.RightKey.Length}) lists differ in length."))

            (cond.LeftKey, cond.RightKey)
            ||> List.map2 (fun l r -> $"l.{l} = r.{r}")
            |> String.concat "\n  AND "
        else
            raise (
                DashSpecFlowException(
                    $"composite source '{source.Id}': join needs 'on' or paired 'left_key'/'right_key'."))

    /// <summary>The FROM-body of a source: a view name, a parenthesized SQL body,
    /// an aliased join, or a union derived table. None = the source has no SQL
    /// material (an xlsx / sql-file leaf) — callers skip it. <paramref name="unionColumns"/>
    /// is the canonical projection (fields of the union row type, ADR-0126 §1) —
    /// a managed union never rides SELECT *.</summary>
    let fromClause (module': DashflowModule) (source: DashflowSourceDef) (unionColumns: string option) : string option =
        match source.Origin with
        | SourceOrigin.Leaf(kind, value, _) -> leafFrom kind value
        | SourceOrigin.InnerJoin(_, _, cond)
        | SourceOrigin.LeftJoin(_, _, cond)
        | SourceOrigin.RightJoin(_, _, cond)
        | SourceOrigin.FullJoin(_, _, cond) ->
            let kind =
                match source.Origin with
                | SourceOrigin.InnerJoin _ -> "INNER"
                | SourceOrigin.LeftJoin _ -> "LEFT"
                | SourceOrigin.RightJoin _ -> "RIGHT"
                | _ -> "FULL"

            let (left, right) = sidesOf source module'
            let condition = conditionOf source cond
            Some $"{left} AS l {kind} JOIN {right} AS r\nON {condition}"
        | SourceOrigin.UnionAll _ ->
            let (left, right) = sidesOf source module'
            let columns = unionColumns |> Option.defaultValue "*"
            Some $"(\nSELECT {columns} FROM {left}\nUNION ALL\nSELECT {columns} FROM {right}\n) AS u"

    /// <summary>The reader-side query body for a composite source: a plain SELECT
    /// over the composed FROM, ready to ride the legacy card datasource (Sql/Query
    /// carrier). Leaf sources return None — they materialize as before.</summary>
    let selectBody (module': DashflowModule) (source: DashflowSourceDef) : string option =
        match source.Origin with
        | SourceOrigin.Leaf _ -> None
        | _ ->
            match fromClause module' source None with
            | Some from -> Some $"SELECT * FROM {from}"
            | None -> None
