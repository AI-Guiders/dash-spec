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

    /// Reader positioned after optional report title, before report body.
    let scanReportBody (reader: TokenReader) =
        if System.String.IsNullOrWhiteSpace reader.ModuleSource then
            scanModuleText ""
        else
            ReferenceScanSkip.scanReportAtReader reader
