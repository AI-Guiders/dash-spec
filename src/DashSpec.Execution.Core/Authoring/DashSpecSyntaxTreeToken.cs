using DashSpecSyntaxKind = DashSpec.Modeling.Parse.Syntax.DashSpecSyntaxKind;
namespace DashSpec.Execution.Authoring;

/// <summary>Leaf token attached to a syntax tree node.</summary>
public readonly record struct DashSpecSyntaxTreeToken(
    int Start,
    int Length,
    string Text,
    DashSpecSyntaxKind Kind);
