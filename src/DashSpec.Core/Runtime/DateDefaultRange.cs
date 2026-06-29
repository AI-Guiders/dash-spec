using DashSpec.Core.Parsing;

namespace DashSpec.Core.Runtime;

/// <summary>
/// Parses date filter defaults from .dashspec (e.g. <c>-7d..today</c>, <c>2026-06-01..2026-06-30</c>).
/// No magic preset names — range is fully spelled in the spec.
/// </summary>
public static partial class DateDefaultRange
{
    public static DateRangeValue Resolve(string expression, DateOnly todayUtc)
    {
        var (fromToken, toToken) = SplitRange(expression);
        var from = ResolveBound(fromToken, todayUtc);
        var to = ResolveBound(toToken, todayUtc);
        if (from > to)
        {
            throw new DashSpecParseException(
                $"Date default range is inverted: '{expression}' ({from} > {to}).");
        }

        return new DateRangeValue(from, to);
    }

    public static void ValidateSyntax(string expression)
    {
        var (fromToken, toToken) = SplitRange(expression);
        _ = ResolveBoundShape(fromToken);
        _ = ResolveBoundShape(toToken);
    }

    public static void ValidateSingleDayDefault(string expression)
    {
        var (fromToken, toToken) = SplitRange(expression);
        if (!string.Equals(fromToken, toToken, StringComparison.OrdinalIgnoreCase))
        {
            throw new DashSpecParseException(
                $"Date filter with widget=day requires a single-day default (from..to must match), e.g. today..today, got '{expression.Trim()}'.");
        }
    }

    private static (string From, string To) SplitRange(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new DashSpecParseException(
                "Date filter requires explicit default range, e.g. default -7d..today");
        }

        var parts = expression.Split("..", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            throw new DashSpecParseException(
                $"Date default must be 'from..to' (e.g. -7d..today or 2026-06-01..2026-06-30), got '{expression.Trim()}'.");
        }

        return (parts[0], parts[1]);
    }

    private static BoundKind ResolveBoundShape(string token)
    {
        if (token.Equals("today", StringComparison.OrdinalIgnoreCase))
        {
            return BoundKind.Today;
        }

        if (RelativeDayPattern().IsMatch(token))
        {
            return BoundKind.RelativeDay;
        }

        if (DateOnly.TryParse(token, out _))
        {
            return BoundKind.Absolute;
        }

        throw new DashSpecParseException(
            $"Unknown date bound '{token}'. Use today, -Nd (e.g. -7d), or yyyy-MM-dd.");
    }

    private static DateOnly ResolveBound(string token, DateOnly todayUtc) =>
        ResolveBoundShape(token) switch
        {
            BoundKind.Today => todayUtc,
            BoundKind.RelativeDay => ResolveRelativeDay(token, todayUtc),
            BoundKind.Absolute => DateOnly.Parse(token),
            _ => throw new DashSpecParseException($"Unknown date bound '{token}'."),
        };

    private static DateOnly ResolveRelativeDay(string token, DateOnly todayUtc)
    {
        var match = RelativeDayPattern().Match(token);
        if (!match.Success || !int.TryParse(match.Groups["days"].Value, out var days))
        {
            throw new DashSpecParseException($"Invalid relative day '{token}'. Use form -Nd, e.g. -7d.");
        }

        return todayUtc.AddDays(-days);
    }

    private static System.Text.RegularExpressions.Regex RelativeDayPattern() =>
        RelativeDayRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"^-(?<days>\d+)d$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex RelativeDayRegex();

    private enum BoundKind
    {
        Today,
        RelativeDay,
        Absolute,
    }
}
