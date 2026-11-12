namespace DashSpec.Modeling.Parse.DataFlow

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Core.TransformDeclarations
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing
open DashSpec.Modeling.Parse.Syntax

/// <summary>
/// Projects transform declaration units (<c>module transform &lt;id&gt;</c>) directly from the
/// typed AST via <see cref="UnitGrammar.unwrap"/> (every member is a standard <c>PropertyAssignment</c>
/// with <c>=</c>, including <c>param &lt;name&gt; = &lt;kind&gt;</c>).
/// </summary>
module TransformDeclarationParser =

    let private unquote (raw: string) =
        let t = raw.Trim()
        if t.Length >= 2 && t.[0] = '"' && t.[t.Length - 1] = '"' then
            t.Substring(1, t.Length - 2)
        else
            t

    let parseDeclaration (text: string) : TransformDecl =
        if String.IsNullOrWhiteSpace text then
            invalidArg "text" "Transform declaration text is required."

        let tree = DashSpecSurfaceSyntax.parse text
        let env = UnitGrammar.unwrap DashSpecModuleKind.Transform tree.Root
        let id = env.Id

        let mutable locality = "runtime"
        let mutable requiresBlock = false
        let mutable hint = ""
        let mutable effect = TransformDeclarations.TransformTypeEffect.Opaque
        let params' = ResizeArray<TransformDeclarations.ParamDecl>()

        for prop in env.View.Properties do
            match prop.KeyTokens with
            | [| { LexKind = TokenKind.Ident; Text = kw }; { LexKind = TokenKind.Ident; Text = paramName } |]
                when String.Equals(kw, "param", StringComparison.OrdinalIgnoreCase) ->
                let kind = prop.ValueText.Trim()
                params'.Add({ TransformDeclarations.ParamDecl.Key = paramName; Literals = [ kind ]; Hint = hint })
            | _ ->
                match prop.Key.ToLowerInvariant() with
                | "locality" ->
                    locality <- prop.ValueText.Trim()
                | "type_effect" ->
                    let name = prop.ValueText.Trim()
                    match TransformDeclarations.TransformTypeEffect.tryParse name with
                    | Some parsed -> effect <- parsed
                    | None ->
                        raise (
                            DashSpecInvalidValueException(
                                $"transform '{id}': unknown type_effect '{name}' — expected identity|projection|extend|compose|require_equal|opaque.",
                                "type_effect",
                                prop.Span.Start))
                | "requires_block" ->
                    requiresBlock <- String.Equals(prop.ValueText.Trim(), "true", StringComparison.OrdinalIgnoreCase)
                | "hint" ->
                    hint <- unquote prop.ValueText
                | _ -> ()

        let resolvedParams =
            params'
            |> Seq.map (fun p ->
                if String.IsNullOrWhiteSpace p.Hint && not (String.IsNullOrWhiteSpace hint) then
                    { p with Hint = hint }
                else
                    p)
            |> Seq.toList

        { Id = id
          Locality = locality
          Params = resolvedParams
          RequiresParameterBlock = requiresBlock
          TypeEffect = effect }

    let private stdlibLoaded = ref false
    let private stdlibLock = obj ()

    /// <summary>Lazy stdlib self-registration: on the first call, every embedded
    /// <c>stdlib/transforms/**.ds</c> unit of this assembly is parsed and registered.</summary>
    let ensureStdlibRegistered () =
        lock stdlibLock (fun () ->
            if not stdlibLoaded.Value then
                stdlibLoaded.Value <- true
                let assembly = Reflection.Assembly.GetExecutingAssembly()
                let prefix = "stdlib/transforms/"

                for name in assembly.GetManifestResourceNames() do
                    if name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) then
                        use stream = assembly.GetManifestResourceStream name
                        use textReader = new IO.StreamReader(stream)
                        TransformDeclarations.register (parseDeclaration (textReader.ReadToEnd())))
