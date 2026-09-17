using DashSpec.Core.Authoring;
using FsharpSyntax = DashSpec.Modeling.Parse.Syntax;

namespace DashSpec.Execution.Parsing;

/// <summary>Wire F# syntax classifier into Core pipeline (DASHSPEC-ADR-0053).</summary>
internal static class DocumentSyntaxRegistration
{
    internal static void Register()
    {
        SyntaxClassificationBridge.Classify = text =>
            FsharpSyntax.DashSpecSyntaxClassifier.classify(text)
                .Select(span => new DashSpecSyntaxSpan(span.Start, span.Length, (DashSpecSyntaxKind)span.Kind))
                .ToArray();
    }
}
