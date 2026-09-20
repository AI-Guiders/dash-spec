namespace DashSpec.Core.Authoring;

/// <summary>Typed discriminant for <see cref="DashSpecSyntaxNode"/> (mirrors F# DashSpecAstNode DU).</summary>
public enum DashSpecAstNodeKind
{
    CompilationUnit = 0,
    ModuleDeclaration = 1,
    BlockDeclaration = 2,
    CardReference = 3,
    EndBlock = 4,
    Line = 5,
    BlankLine = 6,
}
