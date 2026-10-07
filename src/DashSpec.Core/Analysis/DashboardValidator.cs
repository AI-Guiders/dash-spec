using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

namespace DashSpec.Core.Analysis;

internal static class DashboardValidator
{
    public static void Validate(DashboardDocument document)
    {
        var errors = DashboardValidationCollector.Collect(document);
        if (errors.Count == 0)
        {
            return;
        }

        throw new DashSpecParseException(FormatErrors(errors));
    }

    private static string FormatErrors(IReadOnlyList<string> errors) =>
        errors.Count == 1
            ? errors[0]
            : string.Join('\n', errors);
}
