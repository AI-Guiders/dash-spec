namespace DashSpec.Core.Authoring;

/// <summary>Structural node in a DashSpec syntax tree.</summary>
public sealed class DashSpecSyntaxNode
{
    public DashSpecSyntaxNode(
        DashSpecAstNodeKind kind,
        int start,
        int length,
        IReadOnlyList<DashSpecSyntaxTreeToken> tokens,
        IReadOnlyList<DashSpecSyntaxNode> children)
    {
        Kind = kind;
        Start = start;
        Length = length;
        Tokens = tokens;
        Children = children;
    }

    public DashSpecAstNodeKind Kind { get; }

    public int Start { get; }

    public int Length { get; }

    public int End => Start + Length;

    public IReadOnlyList<DashSpecSyntaxTreeToken> Tokens { get; }

    public IReadOnlyList<DashSpecSyntaxNode> Children { get; }

    public bool Contains(int offset) => offset >= Start && offset < End;

    public DashSpecSyntaxNode? FindNodeAt(int offset)
    {
        if (!Contains(offset))
        {
            return null;
        }

        var deepest = this;
        foreach (var child in Children)
        {
            if (child.FindNodeAt(offset) is { } deeper)
            {
                deepest = deeper;
            }
        }

        return deepest;
    }
}
