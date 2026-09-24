using System.Text;

namespace DashSpec.Abstractions.Query;

/// <summary>
/// SQL dialect fragments for filter compilation (<c>@sqldialect</c>).
/// Connectors execute <see cref="CompiledQuery"/>; dialect backends shape SQL text only.
/// </summary>
public interface ISqlDialectBackend
{
    /// <summary>Registry id: <c>tsql</c>, <c>postgres</c>, <c>sqlite</c>, …</summary>
    string Id { get; }

    SqlRowLimitStyle RowLimitStyle { get; }

    /// <summary>Exclusive upper bound for inclusive date-range filters (<c>col &lt; expr</c>).</summary>
    string FormatDateRangeUpperExclusive(string toParameter);

    void AppendSelect(StringBuilder sql, SqlSelectParts parts);
}
