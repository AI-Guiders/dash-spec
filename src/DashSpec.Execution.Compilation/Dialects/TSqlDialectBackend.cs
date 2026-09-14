using System.Text;
using DashSpec.Abstractions.Query;

namespace DashSpec.Execution.Compilation.Dialects;

public class TSqlDialectBackend : ISqlDialectBackend
{
    public virtual string Id => "tsql";

    public virtual SqlRowLimitStyle RowLimitStyle => SqlRowLimitStyle.TopPrefix;

    public virtual string FormatDateRangeUpperExclusive(string toParameter) =>
        $"DATEADD(day, 1, {toParameter})";

    public virtual void AppendSelect(StringBuilder sql, SqlSelectParts parts)
    {
        sql.Append("SELECT ");
        if (parts.TableLimit > 0 && RowLimitStyle is SqlRowLimitStyle.TopPrefix)
        {
            sql.Append("TOP ").Append(parts.TableLimit).Append(' ');
        }

        sql.Append(parts.SelectList);
        sql.Append(" FROM ").Append(parts.FromClause);
        sql.Append(' ').Append(parts.WhereClause);
        if (!string.IsNullOrEmpty(parts.GroupBy))
        {
            sql.Append(' ').Append(parts.GroupBy);
        }

        sql.Append(' ').Append(parts.OrderBy);

        if (parts.TableLimit > 0 && RowLimitStyle is SqlRowLimitStyle.TrailingLimit)
        {
            sql.Append(" LIMIT ").Append(parts.TableLimit);
        }
    }
}
