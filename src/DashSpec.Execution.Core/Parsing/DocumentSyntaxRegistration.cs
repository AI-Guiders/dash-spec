using DashSpec.Core.Authoring;
using FsharpSyntax = DashSpec.Modeling.Parse.Syntax;

namespace DashSpec.Execution.Parsing;

/// <summary>Wire F# syntax tree + classifier into Core pipeline (DASHSPEC-ADR-0053).</summary>
internal static class DocumentSyntaxRegistration
{
    internal static void Register()
    {
        SyntaxClassificationBridge.Classify = text =>
            FsharpSyntax.DashSpecSyntaxClassifier.classify(text)
                .Select(span => new DashSpecSyntaxSpan(span.Start, span.Length, (DashSpecSyntaxKind)span.Kind))
                .ToArray();

        SyntaxTreeBridge.Parse = text => MapTree(FsharpSyntax.SyntaxTree.parse(text));
    }

    static DashSpecSyntaxTree MapTree(FsharpSyntax.ParseTree tree) =>
        new(tree.Text, MapNode(tree.Root));

    static DashSpecSyntaxNode MapNode(FsharpSyntax.DashSpecAstNode node)
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
