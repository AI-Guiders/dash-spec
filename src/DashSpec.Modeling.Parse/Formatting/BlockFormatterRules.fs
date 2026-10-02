namespace DashSpec.Modeling.Parse.Formatting

module BlockFormatterRules =

    type LineKind = BlockSurfaceLineClassifier.LineKind

    let classifyLine trimmed = BlockSurfaceLineClassifier.classifyTrimmedLine trimmed
