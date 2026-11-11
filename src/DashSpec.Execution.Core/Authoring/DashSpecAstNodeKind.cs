namespace DashSpec.Execution.Authoring;

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
    ImportDeclaration = 7,
    TypeFieldDeclaration = 8,
    PropertyAssignment = 9,
    PortDeclaration = 10,
    FlowLinkDeclaration = 11,
    ClauseDeclaration = 12,
    GridRowDeclaration = 13,
    TableRowDeclaration = 14,
}
