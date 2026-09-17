namespace DashSpec.Core.Authoring;

/// <summary>Parse tree for DashSpec source text (IDE foundation).</summary>
public sealed class DashSpecSyntaxTree
{
    public DashSpecSyntaxTree(string text, DashSpecSyntaxNode root)
    {
        Text = text;
        Root = root;
    }

    public string Text { get; }

    public DashSpecSyntaxNode Root { get; }

    public DashSpecSyntaxNode? FindNodeAt(int offset) => Root.FindNodeAt(offset);

    public IReadOnlyList<DashSpecSyntaxSpan> ClassifiedSpans() =>
        DashSpecSyntaxPipeline.Classify(Text);
}
