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
