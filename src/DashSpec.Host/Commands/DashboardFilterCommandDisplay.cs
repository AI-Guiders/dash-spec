#nullable enable
using AIGuiders.Platform.CommandPlane;

namespace DashSpec.Host.Commands;

/// <summary>CLI-facing labels for dash-ccl (no leading slashes).</summary>
internal static class DashboardFilterCommandDisplay
{
    public const string Prompt = ">";
    public const string EmptyPlaceholder = "select · view …";
    public const string AcceptCompletionHint = "Ctrl+Space — выбрать";

    public static string VisiblePrefix => $"{Prompt} ";

    public static string AcceptHint(string action) => $"Ctrl+Space — {action}";

    public static SlashInputGuidance ForCli(SlashInputGuidance guidance) =>
        guidance with
        {
            Breadcrumb = FormatBreadcrumb(guidance.Breadcrumb),
            Placeholder = FormatPlaceholder(guidance.Placeholder),
        };

    public static string FormatCommand(string text)
    {
        var line = text.Trim();
        if (line.StartsWith('/'))
        {
            line = line[1..].TrimStart();
        }

        return line;
    }

    public static string FormatSuggestion(SlashCompletionItem item, DashboardFilterContext context) =>
        FormatSuggestionParts(item, context).Primary;

    public static (string Primary, string? Secondary) FormatSuggestionParts(
        SlashCompletionItem item,
        DashboardFilterContext context)
    {
        if (!string.IsNullOrWhiteSpace(item.PickValue))
        {
            return (item.PickValue, null);
        }

        if (string.IsNullOrWhiteSpace(item.StepSegment))
        {
            return (FormatCommand(item.InsertText.TrimEnd()), null);
        }

        if (item.StepSegment.Equals(FilterCommandPaths.RootVerb, StringComparison.OrdinalIgnoreCase)
            || item.StepSegment.Equals(ViewCommandPaths.RootVerb, StringComparison.OrdinalIgnoreCase)
            || item.StepSegment.Equals(FilterCommandPaths.FilterBranch, StringComparison.OrdinalIgnoreCase)
            || item.StepSegment.Equals(FilterCommandPaths.ReportBranch, StringComparison.OrdinalIgnoreCase)
            || item.StepSegment.Equals(FilterCommandPaths.PageBranch, StringComparison.OrdinalIgnoreCase))
        {
            return (item.StepSegment, null);
        }

        if (item.SlashPath.StartsWith("view ", StringComparison.OrdinalIgnoreCase))
        {
            var parts = item.SlashPath.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 3)
            {
                return (item.Help, parts[2]);
            }

            if (parts.Length >= 2)
            {
                return (item.Help, parts[1]);
            }
        }

        var catalogEntry = context.CatalogEntries.FirstOrDefault(entry =>
            entry.Id.Equals(item.StepSegment, StringComparison.OrdinalIgnoreCase));
        if (catalogEntry is not null)
        {
            return (catalogEntry.Title, catalogEntry.Id);
        }

        var page = context.ReportPages.FirstOrDefault(reportPage =>
            reportPage.Id.Equals(item.StepSegment, StringComparison.OrdinalIgnoreCase));
        if (page is not null)
        {
            return (page.Title ?? page.Id, page.Id);
        }

        if (context.ToolbarFilterNames.Contains(item.StepSegment, StringComparer.OrdinalIgnoreCase))
        {
            var label = DashboardCommandEntityResolver.ResolveFilterLabel(context, item.StepSegment);
            return (label, item.StepSegment);
        }

        if (!string.IsNullOrWhiteSpace(item.Help))
        {
            return (item.Help, item.StepSegment);
        }

        return (item.StepSegment, null);
    }

    public static string FormatTreeBreadcrumb(string typedBody)
    {
        var text = typedBody.Trim();
        return text.Length == 0 ? "команда" : text;
    }

    static string FormatBreadcrumb(string breadcrumb)
    {
        if (string.IsNullOrWhiteSpace(breadcrumb))
        {
            return "команда";
        }

        return breadcrumb.TrimStart('/').Replace(" › ", " ");
    }

    static string FormatPlaceholder(string placeholder) =>
        placeholder switch
        {
            "Type a command path" => EmptyPlaceholder,
            "Continue typing the command path" => EmptyPlaceholder,
            _ when placeholder.StartsWith("Next: ", StringComparison.Ordinal) =>
                placeholder["Next: ".Length..].Trim(),
            _ => FormatCommand(placeholder),
        };
}
