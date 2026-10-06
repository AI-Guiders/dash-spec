namespace DashSpec.Modeling.Parse.Document

open System
open System.Collections.Generic
open System.Text.RegularExpressions
open DashSpec.Modeling.Parse.Lexing

/// Collects diagram / row type references from module text (lookahead for module linker).
module ReportReferenceScanner =

    let private diagramPattern = Regex(@"\bdiagram\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.IgnoreCase)
    let private rowsPattern = Regex(@"\brows\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.IgnoreCase)

    let scanModuleText (text: string) =
        let diagramIds = HashSet<string>(StringComparer.OrdinalIgnoreCase)
        let rowTypes = HashSet<string>(StringComparer.OrdinalIgnoreCase)

        if not (String.IsNullOrWhiteSpace text) then
            for match' in diagramPattern.Matches text do
                diagramIds.Add match'.Groups.[1].Value |> ignore

            for match' in rowsPattern.Matches text do
                rowTypes.Add match'.Groups.[1].Value |> ignore

        diagramIds :> ISet<_>, rowTypes :> ISet<_>

    /// Reader positioned after optional report title; uses full module source (non-mutating).
    let scanReportBody (reader: TokenReader) = scanModuleText reader.ModuleSource
