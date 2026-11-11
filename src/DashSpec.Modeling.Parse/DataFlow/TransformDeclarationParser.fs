namespace DashSpec.Modeling.Parse.DataFlow

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Core.TransformDeclarations
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

/// <summary>
/// Parses transform declaration units (ADR-0126 slice 0.5) — the stdlib's
/// vocabulary. A unit is a flat header + keys:
/// <c>module transform &lt;id&gt;</c>, <c>namespace …</c> (metadata),
/// <c>locality = database|runtime</c>, <c>requires_block = true|false</c>,
/// <c>param &lt;key&gt;: &lt;kind&gt;</c> (kind: ident|string|timeshift|iana),
/// <c>hint = "…"</c> (the diagnostic for a literal mismatch). The units are
/// embedded in this assembly (<c>stdlib/transforms/**.ds</c>) and self-register
/// lazily on the first vocabulary query — direct parser use sees them without any
/// host bootstrap.
/// </summary>
module TransformDeclarationParser =

    let parseDeclaration (text: string) : TransformDecl =
        let reader = ParserUtilities.createReader text
        reader.SkipFileDirectives()
        reader.SkipNewlines()

        reader.ExpectKeyword "module" |> ignore
        reader.ExpectKeyword "transform" |> ignore
        let id = reader.ReadIdent()
        reader.SkipNewlines()

        // namespace line — metadata only; the declaration carries the id itself.
        if reader.TryKeyword "namespace" then
            while not (reader.IsOnNewline()) && not reader.IsEof do
                reader.Advance() |> ignore

        reader.SkipNewlines()

        let mutable locality = "runtime"
        let mutable requiresBlock = false
        let mutable hint = ""
        let mutable effect = TransformDeclarations.TransformTypeEffect.Opaque
        let params' = ResizeArray<TransformDeclarations.ParamDecl>()

        while not reader.IsEof do
            if reader.IsOnNewline() then
                reader.SkipNewlines()
            elif reader.TryKeyword "locality" then
                reader.Expect TokenKind.Eq |> ignore
                locality <- reader.ReadIdent()
            elif reader.TryKeyword "type_effect" then
                reader.Expect TokenKind.Eq |> ignore
                let name = reader.ReadIdent()

                match TransformDeclarations.TransformTypeEffect.tryParse name with
                | Some parsed -> effect <- parsed
                | None ->
                    raise (
                        DashSpecParseException(
                            $"transform '{id}': unknown type_effect '{name}' — expected identity|projection|extend|compose|require_equal|opaque."))
            elif reader.TryKeyword "requires_block" then
                reader.Expect TokenKind.Eq |> ignore
                requiresBlock <- reader.ReadIdent().Equals("true", StringComparison.OrdinalIgnoreCase)
            elif reader.TryKeyword "hint" then
                reader.Expect TokenKind.Eq |> ignore
                hint <- reader.ReadString()
            elif reader.TryKeyword "param" then
                let key = reader.ReadIdent()
                reader.Expect TokenKind.Colon |> ignore
                let kind = reader.ReadIdent()
                params'.Add({ TransformDeclarations.ParamDecl.Key = key; Literals = [ kind ]; Hint = hint })
            else
                reader.Advance() |> ignore

        { Id = id
          Locality = locality
          Params = List.ofSeq params'
          RequiresParameterBlock = requiresBlock
          TypeEffect = effect }

    let private stdlibLoaded = ref false
    let private stdlibLock = obj ()

    /// <summary>Lazy stdlib self-registration: on the first call, every embedded
    /// <c>stdlib/transforms/**.ds</c> unit of this assembly is parsed and registered.
    /// Idempotent; the flag flips inside the lock — a concurrent first parse must
    /// never observe a half-empty vocabulary (Blazor prerender + circuit parse the
    /// document in parallel).</summary>
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
