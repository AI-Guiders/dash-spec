namespace DashSpec.Modeling.Authoring

open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Document
open DashSpec.Modeling.Parse.Syntax

/// Single authoring parse entry: surface projection (spans/tokens) + Modeling IR when semantic parse succeeds.
type DashSpecAuthoringSession =
    { Text: string
      SurfaceTree: ParseTree
      ConceptGraph: DashSpecConceptGraph
      Document: DashboardDocument option }

[<RequireQualifiedAccess>]
module DashSpecAuthoringEntry =

    let private tryParseDocument (text: string) (specDirectory: string option) =
        try
            Some(DashboardComposer.parse text specDirectory DashSpecParseOptions.defaultOptions)
        with :? DashSpecParseException ->
            None

    /// Lex once; build outline graph; attach <see cref="DashboardDocument"/> when IR parse succeeds.
    let parse (text: string) (specDirectory: string option) : DashSpecAuthoringSession =
        let surfaceTree = DashSpecSurfaceSyntax.parse text
        let document = tryParseDocument text specDirectory
        let conceptGraph =
            { DashSpecConceptGraphBuilder.build surfaceTree with
                Document = document }

        { Text = text
          SurfaceTree = surfaceTree
          ConceptGraph = conceptGraph
          Document = document }

    let parseAndBuild (text: string) (specDirectory: string option) : DashSpecConceptGraph * ProfileLawDiagnostic list =
        let session = parse text specDirectory
        session.ConceptGraph, DashSpecInvariantLaws.all session.ConceptGraph
