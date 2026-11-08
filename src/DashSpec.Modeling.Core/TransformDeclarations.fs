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

    type TransformDecl = {
        Id: string
        /// "database" | "runtime" — the layering invariant's input.
        Locality: string
        Params: ParamDecl list
        RequiresParameterBlock: bool
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

    /// The literal requirement for a parameter key of a transform; unlisted keys
    /// fall back to the generic assignment reader (empty literals list).
    let requirementFor (id: string) (key: string) =
        match tryFind id with
        | None -> None
        | Some decl ->
            decl.Params
            |> List.tryFind (fun p -> String.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase))
            |> Option.map (fun p -> p.Literals, p.Hint)
