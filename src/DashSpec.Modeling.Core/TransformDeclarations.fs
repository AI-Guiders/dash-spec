namespace DashSpec.Modeling.Core

open System
open System.Collections.Concurrent

/// <summary>
/// Transform declarations (ADR-0126 slice 0.5): the parse-time vocabulary and
/// locality of transform steps. The single source of truth is the stdlib's
/// declaration units (<c>stdlib/transforms/**.ds</c>, embedded in the parser
/// assembly and self-registered lazily); F# hardcoded lists are gone. Plugins
/// register their own declarations through <see cref="register"/> — the language
/// extends by declaration, the parser learns no names.
/// </summary>
module TransformDeclarations =

    /// A declared parameter: accepted literal kinds (any | ident | string |
    /// timeshift | iana — see the parser's literalTokenKind map) and whether the
    /// transform requires a parameter block at all.
    type ParamDecl = {
        Key: string
        Literals: string list
        Hint: string
    }

    /// <summary>
    /// The type-effect algebra (ADR-0127 §1.1): how a transform's step changes the
    /// row type flowing through it. The algebra is CLOSED — five combinators cover
    /// everything expressible over rows (relational algebra is closed); the
    /// transform VOCABULARY is open (stdlib units, plugin declarations), and every
    /// new transform must pick one of these effects or declare <c>Opaque</c>
    /// (inference disabled for that node, explicit output type required).
    /// Adding a sixth combinator is an ADR-level decision, not a walk-by.
    /// </summary>
    type TransformTypeEffect =
        /// <c>T -> T</c> — schema unchanged, the same RowTypeDef flows on.
        | Identity
        /// <c>T -> T'</c> — relational aggregation: keeps the group-by field,
        /// produces the aggregate column (SQL contract: the result column is
        /// always <c>value</c> — count → int, else the value column's type).
        | Projection
        /// <c>T -> T ∪ col</c> — all fields kept, one column added.
        | Extend
        /// <c>T_left, T_right -> T'</c> — schema composition (join).
        | Compose
        /// <c>T, T -> T</c> — both inputs must be structurally equal.
        | RequireEqual
        /// No inference — the node requires an explicit output row type.
        | Opaque

    module TransformTypeEffect =
        let tryParse (name: string) =
            match name.Trim().ToLowerInvariant() with
            | "identity" -> Some Identity
            | "projection" -> Some Projection
            | "extend" -> Some Extend
            | "compose" -> Some Compose
            | "require_equal" -> Some RequireEqual
            | "opaque" -> Some Opaque
            | _ -> None

        let toString (effect: TransformTypeEffect) =
            match effect with
            | Identity -> "identity"
            | Projection -> "projection"
            | Extend -> "extend"
            | Compose -> "compose"
            | RequireEqual -> "require_equal"
            | Opaque -> "opaque"

    type TransformDecl = {
        Id: string
        /// "database" | "runtime" — the layering invariant's input.
        Locality: string
        Params: ParamDecl list
        RequiresParameterBlock: bool
        /// ADR-0127: the declared type-effect (defaults to Opaque for plugin
        /// declarations that predate the field — inference skips them).
        TypeEffect: TransformTypeEffect
    }

    let private byId = ConcurrentDictionary<string, TransformDecl>(StringComparer.OrdinalIgnoreCase)

    /// <summary>Register a declaration (last wins). Called by the parser's lazy
    /// stdlib self-registration and by plugin transform loaders.</summary>
    let register (decl: TransformDecl) = byId.[decl.Id] <- decl

    let registered () = byId.Values |> List.ofSeq

    let tryFind (id: string) =
        match byId.TryGetValue id with
        | true, decl -> Some decl
        | _ -> None

    let isKnown (id: string) = byId.ContainsKey id

    /// Locality of a declared transform ("database"/"runtime"); undeclared ids are
    /// unknown and never reach here (the vocabulary check rejects them at parse).
    let localityOf (id: string) =
        match tryFind id with
        | Some decl -> decl.Locality
        | None -> "database"

    /// ADR-0127: the declared type-effect; undeclared ids default to Opaque
    /// (inference never runs for an unknown transform).
    let effectOf (id: string) =
        match tryFind id with
        | Some decl -> decl.TypeEffect
        | None -> Opaque

    /// The literal requirement for a parameter key of a transform; unlisted keys
    /// fall back to the generic assignment reader (empty literals list).
    let requirementFor (id: string) (key: string) =
        match tryFind id with
        | None -> None
        | Some decl ->
            decl.Params
            |> List.tryFind (fun p -> String.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase))
            |> Option.map (fun p -> p.Literals, p.Hint)
