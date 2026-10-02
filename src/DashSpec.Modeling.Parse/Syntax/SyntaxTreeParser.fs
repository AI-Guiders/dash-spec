namespace DashSpec.Modeling.Parse.Syntax

/// Back-compat alias; SSOT is <see cref="DashSpecSurfaceSyntax"/>.
module SyntaxTreeParser =

    let parse text = DashSpecSurfaceSyntax.parse text
