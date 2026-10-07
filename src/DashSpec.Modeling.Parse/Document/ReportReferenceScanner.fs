namespace DashSpec.Modeling.Parse.Document

open DashSpec.Modeling.Parse.Lexing

/// Collects diagram / row-type references for module linking (token walk, not regex).
module ReportReferenceScanner =

    let scanModuleText text = ReferenceScanSkip.scanModuleText text

    /// Reader positioned after optional report title, before report body.
    let scanReportBody (reader: TokenReader) =
        if System.String.IsNullOrWhiteSpace reader.ModuleSource then
            scanModuleText ""
        else
            try
                ReferenceScanSkip.scanReportAtReader reader
            with :? DashSpec.Modeling.Core.DashSpecParseException ->
                ReferenceScanSkip.scanModuleText reader.ModuleSource
