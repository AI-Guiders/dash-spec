namespace DashSpec.Core.Runtime;

/// <summary>Valid date bounds for SQL Server <c>datetime</c>/<c>datetime2</c> parameters.</summary>
public static class SqlDateTimeRange
{
    public static readonly DateOnly Min = new(1753, 1, 1);

    public static readonly DateOnly Max = new(9999, 12, 31);

    public static bool IsQueryable(DateOnly date) => date >= Min && date <= Max;
}
