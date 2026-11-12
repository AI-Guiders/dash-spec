using System.Collections.Concurrent;

namespace DashSpec.Execution.Runtime;

/// <summary>Collect validation messages from registered compilation-stage rule checkers without fail-fast (health, CI).</summary>
public static class DashboardValidationCollector
{
    private static readonly List<(string Id, Action<DashboardDocument, List<string>> Check)> Checkers =
    [
        ("toolbar", (doc, errs) => Try(() => ToolbarAnalyzer.Validate(doc), errs)),
        ("filter_placement", FilterPlacementAnalyzer.CollectErrors),
        ("page", (doc, errs) => Try(() => PageAnalyzer.Validate(doc), errs)),
        ("tab", (doc, errs) => Try(() => TabAnalyzer.Validate(doc), errs)),
        ("row_type", (doc, errs) => Try(() => RowTypeAnalyzer.Validate(doc), errs)),
        ("click_binding", (doc, errs) => Try(() => ClickBindingAnalyzer.Validate(doc), errs)),
        ("layout", (doc, errs) => Try(() => LayoutAnalyzer.Validate(doc), errs)),
        ("date_default", (doc, errs) => Try(() => DateDefaultAnalyzer.Validate(doc), errs)),
    ];

    private static readonly ConcurrentDictionary<string, Action<DashboardDocument, List<string>>> PluginCheckers =
        new(StringComparer.OrdinalIgnoreCase);

    public static void RegisterChecker(string checkerId, Action<DashboardDocument, List<string>> check)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkerId);
        ArgumentNullException.ThrowIfNull(check);
        PluginCheckers[checkerId] = check;
    }

    public static IReadOnlyList<string> Collect(DashboardDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var errors = new List<string>();

        foreach (var (_, check) in Checkers)
        {
            check(document, errors);
        }

        foreach (var (_, check) in PluginCheckers)
        {
            check(document, errors);
        }

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
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            errors.Add(ex.Message);
        }
        catch (DashSpec.Modeling.Core.DashSpecFlowException ex)
        {
            errors.Add(ex.Message);
        }
    }
}
