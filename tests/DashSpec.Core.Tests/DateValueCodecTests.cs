using System.Globalization;
using DashSpec.Core.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class DateValueCodecTests
{
    [Fact]
    public void TryParseWireDay_accepts_iso()
    {
        Assert.True(DateValueCodec.TryParseWireDay("2026-08-03", out var day));
        Assert.Equal(new DateOnly(2026, 8, 3), day);
    }

    [Fact]
    public void TryParseDashedDayMonthYear_accepts_legacy_token()
    {
        Assert.True(DateValueCodec.TryParseDashedDayMonthYear("0003-08-2026", out var day));
        Assert.Equal(new DateOnly(2026, 8, 3), day);
    }

    [Fact]
    public void TryParseWireDay_rejects_ambiguous_locale_strings()
    {
        Assert.False(DateValueCodec.TryParseWireDay("03.08.2026", out _));
    }

    [Fact]
    public void TryParseStoredBucket_reads_iso_datetime_as_utc()
    {
        var bucket = DateValueCodec.TryParseStoredBucket("2026-08-03T11:05:00");
        Assert.NotNull(bucket);
        Assert.Equal(DateTimeKind.Utc, bucket!.Value.Kind);
        Assert.Equal(11, bucket.Value.Hour);
    }

    [Fact]
    public void TryParseStoredDateTime_skips_current_culture_ambiguity()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Assert.True(DateValueCodec.TryParseStoredDateTime("2026-08-03T10:00:00", out var parsed));
            Assert.Equal(new DateTime(2026, 8, 3, 10, 0, 0, DateTimeKind.Utc), parsed);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void FormatWireDay_emits_iso()
    {
        Assert.Equal("2026-08-03", DateValueCodec.FormatWireDay(new DateOnly(2026, 8, 3)));
    }
}
