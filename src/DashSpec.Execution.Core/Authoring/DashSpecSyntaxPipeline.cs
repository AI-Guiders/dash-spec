using DashSpecSyntaxKind = DashSpec.Modeling.Parse.Syntax.DashSpecSyntaxKind;
using FsharpSyntax = DashSpec.Modeling.Parse.Syntax;

namespace DashSpec.Execution.Authoring;

public static class DashSpecSyntaxPipeline
{
    public static IReadOnlyList<DashSpecSyntaxSpan> Classify(string text) =>
        FsharpSyntax.DashSpecSyntaxClassifier.classify(text)
            .Select(span => new DashSpecSyntaxSpan(span.Start, span.Length, (DashSpecSyntaxKind)span.Kind))
            .ToArray();

    public static DashSpecSyntaxTree Parse(string text) =>
        MapTree(FsharpSyntax.SyntaxTree.parse(text));

    private static DashSpecSyntaxTree MapTree(FsharpSyntax.ParseTree tree) =>
        new(tree.Text, MapNode(tree.Root));

    private static DashSpecSyntaxNode MapNode(FsharpSyntax.DashSpecAstNode node)
    {
        var span = FsharpSyntax.DashSpecAst.span(node);
        return new(
            (DashSpecAstNodeKind)FsharpSyntax.DashSpecAst.nodeKind(node),
            span.Start,
            span.Length,
            FsharpSyntax.DashSpecAst.tokens(node)
                .Select(token => new DashSpecSyntaxTreeToken(
                    token.Span.Start,
                    token.Span.Length,
                    token.Text,
                    (DashSpecSyntaxKind)token.Kind))
                .ToArray(),
            FsharpSyntax.DashSpecAst.members(node).Select(MapNode).ToArray());
    }
}
