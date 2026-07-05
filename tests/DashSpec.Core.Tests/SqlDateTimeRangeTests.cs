using DashSpec.Core.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class SqlDateTimeRangeTests
{
    [Theory]
    [InlineData(1753, 1, 1, true)]
    [InlineData(2026, 7, 5, true)]
    [InlineData(9999, 12, 31, true)]
    [InlineData(1752, 12, 31, false)]
    [InlineData(1, 1, 1, false)]
    public void IsQueryable_respects_sql_server_datetime_bounds(int year, int month, int day, bool expected)
    {
        var date = new DateOnly(year, month, day);
        Assert.Equal(expected, SqlDateTimeRange.IsQueryable(date));
    }
}
