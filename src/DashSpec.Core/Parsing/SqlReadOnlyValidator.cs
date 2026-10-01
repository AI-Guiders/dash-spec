namespace DashSpec.Core.Parsing;

/// <summary>
/// Проверка текста <c>datasource sql</c> и имён <c>datasource view</c> при разборе .dashspec.
/// SSOT: <c>DashSpec.Modeling.Parse.DataSource.SqlReadOnlyValidator</c> (F#).
/// </summary>
public static class SqlReadOnlyValidator
{
    public static void ValidateViewReference(string qualifiedName)
    {
        try
        {
            Modeling.Parse.DataSource.SqlReadOnlyValidator.validateViewReference(qualifiedName);
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    public static void ValidateSqlBody(string sql)
    {
        try
        {
            Modeling.Parse.DataSource.SqlReadOnlyValidator.validateSqlBody(sql);
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }
}
