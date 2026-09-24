namespace DashSpec.Abstractions.Query;

/// <summary>How a dialect applies row limits on compiled SELECT queries.</summary>
public enum SqlRowLimitStyle
{
    TopPrefix,
    TrailingLimit,
}
