namespace DashSpec.Abstractions.Query;

/// <summary>Logical SELECT shape emitted by QueryCompiler via dialect backend.</summary>
public readonly record struct SqlSelectParts(
    string SelectList,
    string FromClause,
    string WhereClause,
    string? GroupBy,
    string OrderBy,
    int TableLimit);
