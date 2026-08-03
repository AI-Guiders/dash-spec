using DashSpec.Core.Model;

namespace DashSpec.Core.Layout;

/// <summary>Filter names declared on dashboard toolbar, page toolbars, or card-local blocks.</summary>
public static class PlacedFilterCollector
{
    public static IReadOnlyList<string> Collect(DashboardDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        void Add(string? name)
        {
            if (string.IsNullOrWhiteSpace(name) || !seen.Add(name))
            {
                return;
            }

            result.Add(name);
        }

        foreach (var name in document.DashboardFilters)
        {
            Add(name);
        }

        if (document.Pages is not null)
        {
            foreach (var page in document.Pages)
            {
                if (page.ToolbarBoard is null)
                {
                    continue;
                }

                foreach (var name in page.ToolbarBoard.Rows.SelectMany(row => row))
                {
                    Add(name);
                }
            }
        }

        foreach (var card in document.Cards)
        {
            foreach (var name in card.LocalFilters)
            {
                Add(name);
            }
        }

        return result;
    }
}
