namespace DashSpec.Modeling.Parse

open System
open System.Collections.Concurrent
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Syntax

[<RequireQualifiedAccess>]
type UnitCompileRole =
    | Declaration
    | Consumer
    | Root

type UnitProjectionContext =
    { Text: string
      Envelope: UnitGrammar.UnitEnvelope
      SpecDirectory: string option }

type UnitPluginDescriptor =
    { KindKeyword: string
      ImportKind: ImportKind option
      Role: UnitCompileRole
      Project: UnitProjectionContext -> obj }

type RegistryDocument =
    { Id: string
      Namespace: string option
      Properties: System.Collections.Generic.IReadOnlyDictionary<string, string>
      Columns: string[]
      Rows: System.Collections.Generic.IReadOnlyList<Map<string, string>> }

/// <summary>First-class <c>module registry &lt;id&gt;</c> unit parser (ADR-0131):
/// projects declarative tabular registries (e.g. <c>stdlib/units/units.ds</c>, <c>stdlib/rules/rules.ds</c>,
/// or user-authored registries) directly from the AST.</summary>
module RegistryModuleParser =

    let parseEnvelope (env: UnitGrammar.UnitEnvelope) : RegistryDocument =
        let props = System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        for p in env.View.Properties do
            props.[p.Key] <- p.Value.Text

        let columns =
            env.View.TableRows
            |> Array.tryPick (fun r ->
                match r.Kind with
                | TableRowKind.Header cols -> Some cols
                | _ -> None)
            |> Option.defaultValue [||]

        let rows = ResizeArray<Map<string, string>>()
        match Construct.tryFromAstTable env.Body with
        | Some(Construct.ConstructForm.TableForm tableRows) when tableRows.Length > 1 ->
            let header = tableRows.[0] |> Array.map (fun c -> c.Trim())
            for r in tableRows.[1..] do
                let isSep = r.Length > 0 && r |> Array.forall (fun c -> c.Length > 0 && c |> Seq.forall (fun ch -> ch = '-' || ch = ':'))
                if not isSep then
                    let m =
                        [ for i in 0 .. (min (r.Length - 1) (header.Length - 1)) do
                            let k = header.[i]
                            let v = r.[i].Trim()
                            if k.Length > 0 && v.Length > 0 && v <> "-" then
                                yield k, v ]
                        |> Map.ofList
                    rows.Add m
        | _ -> ()

        { Id = env.Id
          Namespace = env.Namespace
          Properties = props
          Columns = columns
          Rows = rows :> System.Collections.Generic.IReadOnlyList<_> }

    let parse (text: string) : RegistryDocument =
        if String.IsNullOrWhiteSpace text then
            invalidArg "text" "Registry text is required."
        let tree = DashSpecSurfaceSyntax.parse text
        let env = UnitGrammar.unwrap DashSpecModuleKind.Registry tree.Root
        parseEnvelope env

/// <summary>Pure microkernel unit plugin registry: sits directly above <see cref="UnitGrammar"/>
/// and knows zero concrete unit domains. Each vertical domain (built-in or external plugin)
/// registers its own <see cref="UnitPluginDescriptor"/> here.</summary>
module UnitProjector =

    let private plugins =
        ConcurrentDictionary<string, UnitPluginDescriptor>(StringComparer.OrdinalIgnoreCase)

    let register (descriptor: UnitPluginDescriptor) =
        if not (String.IsNullOrWhiteSpace descriptor.KindKeyword) then
            plugins.[descriptor.KindKeyword.Trim()] <- descriptor

    let registerCustomProjector (kind: string) (projector: UnitGrammar.UnitEnvelope -> obj) =
        register
            { KindKeyword = kind
              ImportKind = None
              Role = UnitCompileRole.Consumer
              Project = fun ctx -> projector ctx.Envelope }

    let tryResolve (kindKeyword: string) : UnitPluginDescriptor option =
        if String.IsNullOrWhiteSpace kindKeyword then
            None
        else
            match plugins.TryGetValue(kindKeyword.Trim()) with
            | true, d -> Some d
            | false, _ -> None

    let registeredKinds () : string list =
        plugins.Keys |> Seq.sort |> Seq.toList

    let project (text: string) (specDirectory: string option) : string * string * obj =
        if String.IsNullOrWhiteSpace text then
            invalidArg "text" "Unit text is required."

        let tree = DashSpecSurfaceSyntax.parse text

        match UnitGrammar.tryUnwrapAny tree.Root with
        | None ->
            raise (DashSpecUnitHeaderException("Unit requires 'module <kind> <id>'."))
        | Some env ->
            let kw = DashSpecModuleKind.toKeyword env.Kind
            match tryResolve kw with
            | Some descriptor ->
                let payload =
                    descriptor.Project
                        { Text = text
                          Envelope = env
                          SpecDirectory = specDirectory }
                kw, env.Id, payload
            | None ->
                raise (DashSpecUnitHeaderException($"Unknown module kind '{kw}'.", env.ModuleNode.Span.Start))

    do
        register
            { KindKeyword = "registry"
              ImportKind = Some ImportKind.Registries
              Role = UnitCompileRole.Declaration
              Project = fun ctx -> box (RegistryModuleParser.parseEnvelope ctx.Envelope) }
