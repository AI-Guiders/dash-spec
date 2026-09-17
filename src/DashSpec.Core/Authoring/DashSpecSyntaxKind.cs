namespace DashSpec.Core.Authoring;

/// <summary>Lexer-driven syntax classification kinds (DASHSPEC-ADR-0053).</summary>
public enum DashSpecSyntaxKind
{
    Comment = 0,
    ModuleHeader = 1,
    Keyword = 2,
    EndKeyword = 3,
    String = 4,
    Number = 5,
    Operator = 6,
    Identifier = 7,
    Punctuation = 8,
}
