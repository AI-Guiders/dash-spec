using DashSpec.Core.Model;

namespace DashSpec.Core.Analysis;

internal static class DashboardValidator
{
    public static void Validate(DashboardDocument document)
    {
        ToolbarAnalyzer.Validate(document);
        FilterPlacementAnalyzer.Validate(document);
        TabAnalyzer.Validate(document);
    }
}
