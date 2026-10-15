namespace DashSpec.Modeling.Parse.Document

open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Syntax

/// <summary>Compile API result (ADR-0098 front-end).</summary>
[<CLIMutable>]
type DashSpecCompileResult =
    { Document: DashboardDocument
      Diagnostics: IReadOnlyList<DashSpecDiagnostic> }

/// <summary>Public compile entry — <see cref="DashboardComposer.parse"/> with diagnostics surface.</summary>
module DashSpecCompiler =

    let private ruleDiagnostics (text: string) : DashSpecDiagnostic list =
        DashSpecRuleChecks.findings (DashSpecSurfaceSyntax.parse text).Root
        |> List.map (DashSpecRuleChecks.toDiagnostic text)

    let compile (text: string) (specDirectory: string option) (parseOptions: DashSpecParseOptions) =
        // Runtime compile must match DashboardComposer.parse (tab dashspec merge + completeDocument).
        let document = DashboardComposer.parse text specDirectory parseOptions
        { Document = document
          Diagnostics = ruleDiagnostics text :> IReadOnlyList<_> }

    let compileDefault (text: string) (specDirectory: string option) =
        compile text specDirectory DashSpecParseOptions.defaultOptions

    /// <summary>Diagnostics for editors and tools. Does not throw on bad input: language rules run on the surface tree, and a parse failure becomes DS009.</summary>
    let diagnose (text: string) (specDirectory: string option) (parseOptions: DashSpecParseOptions) : DashSpecDiagnostic list =
        let parseFailure =
            try
                DashboardComposer.parse text specDirectory parseOptions |> ignore
                []
            with :? DashSpecParseException as ex ->
                let offset = if ex.SourceOffset.HasValue then ex.SourceOffset.Value else 0
                let line, character = DashSpecRuleChecks.positionOf text offset

                [ DashSpecDiagnostic.create line character line character ex.Message DashSpecDiagnosticSeverity.Error
                  |> DashSpecDiagnostic.withCode (DashSpecRules.code DashSpecRule.ParseError) ]

        ruleDiagnostics text @ parseFailure
