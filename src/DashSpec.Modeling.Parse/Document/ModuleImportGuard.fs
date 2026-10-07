namespace DashSpec.Modeling.Parse.Document

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Lexing

/// <summary>ADR-0098: <c>import</c> is module-header only; <c>use</c> binds inside blocks.</summary>
module ModuleImportGuard =

    let private isModuleImportPeek (reader: TokenReader) =
        match reader.TryPeekIdent() with
        | Some ident when String.Equals(ident, "import", StringComparison.OrdinalIgnoreCase) ->
            let saved = reader.SavePosition()
            reader.TryKeyword "import" |> ignore
            let isLegacyString = reader.IsAt TokenKind.String
            reader.RestorePosition saved
            not isLegacyString
        | _ -> false

    let rejectModuleImportInBody (reader: TokenReader) (context: string) =
        if isModuleImportPeek reader then
            raise (
                DashSpecParseException(
                    $"{context}: 'import' is only allowed in the module header (before runtime). Use 'use' for palette, presentation, and provider binds."
                )
            )
