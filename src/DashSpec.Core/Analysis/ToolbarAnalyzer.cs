using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

namespace DashSpec.Core.Analysis;

internal static class ToolbarAnalyzer
{
    public static void Validate(DashboardDocument document)
    {
        var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var filter in document.Filters)
        {
            if (string.IsNullOrWhiteSpace(filter.LayoutRef))
            {
                continue;
            }

            if (!refs.Add(filter.LayoutRef))
            {
                throw new DashSpecParseException(
                    $"Duplicate filter ref '{filter.LayoutRef}'.");
            }
        }
    }
}
