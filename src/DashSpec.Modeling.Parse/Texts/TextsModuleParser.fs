namespace DashSpec.Modeling.Parse.Texts

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Syntax

type TextBinding =
    { Target: LanguageObjectRef
      Label: string }

[<CLIMutable>]
type TextsDocument =
    { Id: string
      Namespace: string option
      Language: string option
      Bindings: TextBinding[] }

module TextsModuleParser =

    let private unquote = Construct.unquote

    let private isLabelProperty (prop: string) =
        String.Equals(prop, "label", StringComparison.OrdinalIgnoreCase)
        || String.Equals(prop, "title", StringComparison.OrdinalIgnoreCase)
        || String.Equals(prop, "text", StringComparison.OrdinalIgnoreCase)
        || String.Equals(prop, "value", StringComparison.OrdinalIgnoreCase)

    let private entryDeclaration
        (addBinding: TextBinding -> unit)
        : Construct.Declaration<TextBinding>
        =
        let cellsToBinding (m: Map<string, string>) : TextBinding =
            let rawTarget =
                Construct.tryCell m "target"
                |> Option.orElse (Construct.tryCell m "id")
                |> Option.orElse (Construct.tryCell m "key")
                |> Option.map unquote
                |> Option.defaultWith (fun () ->
                    raise (DashSpecParseException("texts table row requires a 'target' (or 'id') cell.")))

            let label =
                Construct.tryCell m "label"
                |> Option.orElse (Construct.tryCell m "title")
                |> Option.orElse (Construct.tryCell m "text")
                |> Option.orElse (Construct.tryCell m "value")
                |> Option.map unquote
                |> Option.defaultValue ""

            { Target = LanguageObjectRef.parse rawTarget
              Label = label }

        { Name = "texts"
          Vocabulary = [ "target"; "id"; "key"; "label"; "title"; "text"; "value" ]
          CellsToRow = cellsToBinding
          ConstructRow = addBinding }

    let parseEnvelope (env: UnitGrammar.UnitEnvelope) : TextsDocument =
        let bindings = ResizeArray<TextBinding>()
        let mutable language: string option = None

        let decl = entryDeclaration bindings.Add

        Construct.projectAst decl None "target" env.Body

        for p in env.View.Properties do
            let k = p.Key.Trim()
            let v = unquote p.ValueText
            if String.Equals(k, "language", StringComparison.OrdinalIgnoreCase) ||
               String.Equals(k, "culture", StringComparison.OrdinalIgnoreCase) then
                language <- Some v
            elif isLabelProperty p.KeyPath.MemberName && p.KeyPath.IsQualified then
                () // projected via Construct.projectAst DottedForm
            else
                bindings.Add
                    { Target = LanguageObjectRef.ofDotSyntax p.KeyPath
                      Label = v }

        { Id = env.Id
          Namespace = env.Namespace
          Language = language
          Bindings = bindings.ToArray() }

    let parse (text: string) : TextsDocument =
        UnitGrammar.parseUnit DashSpecModuleKind.Texts parseEnvelope text

    let parseFile (path: string) : TextsDocument =
        UnitGrammar.parseUnitFile DashSpecModuleKind.Texts parseEnvelope path
