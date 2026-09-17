namespace DashSpec.Core.Authoring;

/// <summary>Structural syntax tree node kinds (DASHSPEC-ADR-0053 §7).</summary>
public enum DashSpecSyntaxNodeKind
{
    CompilationUnit = 0,
    ModuleDeclaration = 1,
    Block = 2,
    EndBlock = 3,
    Line = 4,
    BlankLine = 5,
}
