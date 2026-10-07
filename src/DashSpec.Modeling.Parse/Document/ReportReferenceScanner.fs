namespace DashSpec.Modeling.Parse.Document

open System
open System.Collections.Generic
open System.IO
open System.Text.RegularExpressions
open DashSpec.Modeling.Parse.Lexing

/// Collects diagram / row-type references for module linking (token walk, not regex).
module ReportReferenceScanner =

    let private dashspecPathRegex =
        Regex(@"\bdashspec\s+""([^""]+)""", RegexOptions.Compiled)

    let scanModuleText text = ReferenceScanSkip.scanModuleText text

    /// Like <see cref="scanModuleText"/>, plus diagram/row refs from tab <c>dashspec "path"</c> modules (ADR-0089 glob link).
    let scanModuleLinkEnvelope (text: string) (specDirectory: string) =
        let diagramIds, rowTypes = scanModuleText text
        let diagrams = HashSet<string>(diagramIds, StringComparer.OrdinalIgnoreCase)
        let rows = HashSet<string>(rowTypes, StringComparer.OrdinalIgnoreCase)

        if not (String.IsNullOrWhiteSpace specDirectory) then
            let dir =
                specDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)

            for dashspecMatch in dashspecPathRegex.Matches text do
                let relative = dashspecMatch.Groups.[1].Value
                let modulePath = Path.GetFullPath(Path.Combine(dir, relative))

                if File.Exists modulePath then
                    let nestedDiagrams, nestedRows = scanModuleText (File.ReadAllText modulePath)

                    for id in nestedDiagrams do
                        diagrams.Add id |> ignore

                    for id in nestedRows do
                        rows.Add id |> ignore

        diagrams, rows

    /// Reader positioned after optional report title, before report body.
    let scanReportBody (reader: TokenReader) =
        if System.String.IsNullOrWhiteSpace reader.ModuleSource then
            scanModuleText ""
        else
            ReferenceScanSkip.scanReportAtReader reader
