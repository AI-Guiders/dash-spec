namespace DashSpec.Modeling.Parse.Document

open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse

/// <summary>Compile API result (ADR-0098 front-end).</summary>
[<CLIMutable>]
type DashSpecCompileResult =
    { Document: DashboardDocument
      Diagnostics: IReadOnlyList<DashSpecDiagnostic> }

/// <summary>Public compile entry — <see cref="DashboardComposer.parse"/> with diagnostics surface.</summary>
module DashSpecCompiler =

    let compile (text: string) (specDirectory: string option) (parseOptions: DashSpecParseOptions) =
        let diagnostics = ResizeArray<DashSpecDiagnostic>()
        // Runtime compile must match DashboardComposer.parse (tab dashspec merge + completeDocument).
        let document = DashboardComposer.parse text specDirectory parseOptions
        { Document = document; Diagnostics = diagnostics :> IReadOnlyList<_> }

    let compileDefault (text: string) (specDirectory: string option) =
        compile text specDirectory DashSpecParseOptions.defaultOptions
