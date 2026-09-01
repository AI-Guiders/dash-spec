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



    public static string FormatSuggestion(ArgCompletionItem item, DashboardFilterContext context) =>

        FormatSuggestionParts(item, context).Primary;



    public static (string Primary, string? Secondary) FormatSuggestionParts(
        ArgCompletionItem item,
        DashboardFilterContext context)
    {
        if (item.Kind == ArgCompletionItemKind.ConstructorEntry)
        {
            return (item.StepSegment ?? item.Help, item.PickValue);
        }

        if (item.Kind == ArgCompletionItemKind.ConstructorStep)
        {
            return (item.StepSegment ?? item.PickValue ?? "", item.PickValue);
        }

        if (!string.IsNullOrWhiteSpace(item.PickValue)
            && item.Kind == ArgCompletionItemKind.Picker)
        {
            return (item.PickValue, null);
        }



        if (string.IsNullOrWhiteSpace(item.StepSegment))

        {

            return (FormatCommand(item.InsertText.TrimEnd()), null);

        }



        if (item.StepSegment.Equals(FilterCommandPaths.RootVerb, StringComparison.OrdinalIgnoreCase)

            || item.StepSegment.Equals(ViewCommandPaths.RootVerb, StringComparison.OrdinalIgnoreCase)

            || item.StepSegment.Equals(ShowCommandPaths.RootVerb, StringComparison.OrdinalIgnoreCase)

            || item.StepSegment.Equals(ShowCommandPaths.HostBranch, StringComparison.OrdinalIgnoreCase)

            || item.StepSegment.Equals(FilterCommandPaths.FilterBranch, StringComparison.OrdinalIgnoreCase)

            || item.StepSegment.Equals(FilterCommandPaths.ReportBranch, StringComparison.OrdinalIgnoreCase)

            || item.StepSegment.Equals(FilterCommandPaths.PageBranch, StringComparison.OrdinalIgnoreCase))

        {

            return (item.StepSegment, null);

        }



        if (TryFormatViewSuggestionParts(item, context, out var viewParts))
        {
            return viewParts;
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

    public static string FormatSuggestionHelp(ArgCompletionItem item, DashboardFilterContext context)
    {
        var commandPath = FormatCommand(item.CommandPath);
        if (!commandPath.StartsWith($"{ViewCommandPaths.RootVerb} ", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(item.StepSegment))
        {
            return item.Help;
        }

        var tokens = commandPath.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length < 3)
        {
            return item.Help;
        }

        var card = context.SwitchableCards.FirstOrDefault(target =>
            target.CardId.Equals(tokens[1], StringComparison.OrdinalIgnoreCase));
        if (card is null)
        {
            return item.Help;
        }

        if (item.StepSegment.Equals(tokens[1], StringComparison.OrdinalIgnoreCase))
        {
            return string.Join(" · ", card.Views.Select(view => view.Label));
        }

        var view = card.Views.FirstOrDefault(option =>
            option.ViewId.Equals(tokens[2], StringComparison.OrdinalIgnoreCase));
        return view?.Label ?? item.Help;
    }

    static bool TryFormatViewSuggestionParts(
        ArgCompletionItem item,
        DashboardFilterContext context,
        out (string Primary, string? Secondary) parts)
    {
        parts = default;
        var commandPath = FormatCommand(item.CommandPath);
        if (!commandPath.StartsWith($"{ViewCommandPaths.RootVerb} ", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(item.StepSegment))
        {
            return false;
        }

        var tokens = commandPath.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length < 3)
        {
            return false;
        }

        var card = context.SwitchableCards.FirstOrDefault(target =>
            target.CardId.Equals(tokens[1], StringComparison.OrdinalIgnoreCase));
        if (card is null)
        {
            return false;
        }

        if (item.StepSegment.Equals(tokens[1], StringComparison.OrdinalIgnoreCase))
        {
            parts = (card.Title, card.CardId);
            return true;
        }

        if (item.StepSegment.Equals(tokens[2], StringComparison.OrdinalIgnoreCase))
        {
            var view = card.Views.FirstOrDefault(option =>
                option.ViewId.Equals(tokens[2], StringComparison.OrdinalIgnoreCase));
            parts = (view is not null ? $"{card.Title} — {view.Label}" : tokens[2], tokens[2]);
            return true;
        }

        return false;
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

