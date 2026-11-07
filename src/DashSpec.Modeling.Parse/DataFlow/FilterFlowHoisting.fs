namespace DashSpec.Modeling.Parse.DataFlow

open System
open DashSpec.Modeling.Core

/// <summary>
/// ADR-0120 execution-locality hoisting: the compiler lifts <c>database</c> nodes
/// of a path to its prefix (they fold into one source query), commuting them past
/// <c>runtime</c> nodes. A lift is legal only when the runtime node does not touch
/// the database node's column (ADR-0079 port typing); otherwise — loud error, never
/// a silent split into two queries. The locality dictionary grows via ADR only.
/// </summary>
module FilterFlowHoisting =

    [<RequireQualifiedAccess>]
    type NodeLocality =
        | Database
        | Runtime

    /// One node of an execution path: id, declared locality, and the columns the
    /// node touches (reads or derives — ADR-0079 supplies this from port types).
    /// For a database (filter) node, <c>TouchedColumns</c> carries the filtered column.
    type ChainNode =
        { Id: string
          Locality: NodeLocality
          TouchedColumns: string list }

    /// Known runtime plugins (ADR-0120 dictionary): everything that shifts or
    /// renames for display. Unknown plugins are a loud error at hoisting time —
    /// the dictionary grows via ADR only, no silent defaults.
    module RuntimePluginRegistry =
        let private runtimePlugins =
            [ "to_zone"
              "time_tz" ]

        let isRuntime (pluginId: string) =
            runtimePlugins
            |> List.exists (fun p -> String.Equals(p, pluginId, StringComparison.OrdinalIgnoreCase))

    /// <summary>Reorder the chain so <c>database</c> nodes form the prefix (stable),
    /// commuting them past runtime nodes. A database node commutes past a runtime
    /// node iff the runtime node touches none of its columns. Blocked lift, or a
    /// database node with no declared column (commutation unprovable), is a loud
    /// <see cref="DashSpecParseException"/> with the exact nodes and column.</summary>
    let hoist (chain: ChainNode list) : ChainNode list =
        let touches (runtime: ChainNode) (db: ChainNode) =
            match db.TouchedColumns with
            | [] ->
                raise (
                    DashSpecParseException(
                        $"hoisting: '{db.Id}' (database) declares no filtered column — commutation cannot be proven; declare the column binding (ADR-0079)."
                    )
                )
            | columns ->
                columns
                |> List.exists (fun c ->
                    runtime.TouchedColumns
                    |> List.exists (fun r -> String.Equals(r, c, StringComparison.OrdinalIgnoreCase)))

        let databaseNodes = chain |> List.filter (fun n -> n.Locality = NodeLocality.Database)
        let runtimeNodes = chain |> List.filter (fun n -> n.Locality = NodeLocality.Runtime)

        // A database node already standing before every runtime node needs no lift.
        // Otherwise it must commute past each runtime node that stands before it.
        for db in databaseNodes do
            let blockers =
                chain
                |> List.takeWhile (fun n -> not (obj.ReferenceEquals(n, db)))
                |> List.filter (fun n -> n.Locality = NodeLocality.Runtime && touches n db)

            match blockers with
            | [] -> ()
            | blocker :: _ ->
                let column =
                    match db.TouchedColumns with
                    | c :: _ -> c
                    | [] -> "?"

                raise (
                    DashSpecParseException(
                        $"hoisting: '{db.Id}' (database) cannot be lifted into the source query — its column '{column}' is touched by '{blocker.Id}' (runtime) earlier on the path. Change the topology or declare the filter runtime."
                    )
                )

        databaseNodes @ runtimeNodes
