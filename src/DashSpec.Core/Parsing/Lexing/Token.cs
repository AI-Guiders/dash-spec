namespace DashSpec.Core.Parsing;

internal readonly record struct Token(TokenKind Kind, string Value, int Start, int Length);

internal readonly record struct ColumnBindingValue(string Column, string? Alias);
