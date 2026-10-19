using DashSpecSyntaxKind = DashSpec.Modeling.Parse.Syntax.DashSpecSyntaxKind;
namespace DashSpec.Execution.Authoring;

/// <summary>Classified syntax span from <see cref="DashSpecSyntaxPipeline"/>.</summary>
public readonly record struct DashSpecSyntaxSpan(int Start, int Length, DashSpecSyntaxKind Kind);
