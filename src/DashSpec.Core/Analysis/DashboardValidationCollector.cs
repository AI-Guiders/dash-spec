using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

namespace DashSpec.Core.Analysis;

/// <summary>Collect validation messages without fail-fast (health, CI).</summary>
public static class DashboardValidationCollector
{
    public static IReadOnlyList<string> Collect(DashboardDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var errors = new List<string>();
        Try(() => ToolbarAnalyzer.Validate(document), errors);
        FilterPlacementAnalyzer.CollectErrors(document, errors);
        Try(() => PageAnalyzer.Validate(document), errors);
        Try(() => TabAnalyzer.Validate(document), errors);
        Try(() => RowTypeAnalyzer.Validate(document), errors);
        return errors;
    }

    private static void Try(Action validate, List<string> errors)
    {
        try
        {
            validate();
        }
        catch (DashSpecParseException ex)
        {
            errors.Add(ex.Message);
        }
    }
}
