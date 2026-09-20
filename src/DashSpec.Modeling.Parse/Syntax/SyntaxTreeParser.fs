namespace DashSpec.Modeling.Parse.Syntax

/// Back-compat entry; SSOT is <see cref="DashSpecAstParser"/>.
module SyntaxTreeParser =

    let parse text = DashSpecAstParser.parse text
