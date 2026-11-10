namespace DashSpec.Execution.Runtime;

/// <summary>Compile-time date default contracts: every filter default expression
/// must parse as a from..to range with recognizable bounds (DateDefaultRange
/// syntax), and a widget=day filter must default to a single day. Card-local
/// filters resolve from the same document filter set — one pass covers both.
/// The expression is spec text — an unknown bound or a mismatched range is a
/// compile defect, not a first-render surprise.</summary>
internal static class DateDefaultAnalyzer
{
    public static void Validate(DashboardDocument document)
    {
        foreach (var filter in document.Filters)
        {
            // A field filter's default is a value selection (e.g. scale=day lands
            // here), not a date range — only date filters speak from..to.
            if (filter.Kind is not FilterKind.Date)
            {
                continue;
            }

            var expression = filter.DefaultExpression?.Value;
            if (string.IsNullOrWhiteSpace(expression))
            {
                continue;
            }

            DateDefaultRange.ValidateSyntax(expression);

            if (filter.IsDayWidget)
            {
                DateDefaultRange.ValidateSingleDayDefault(expression);
            }
        }
    }
}
