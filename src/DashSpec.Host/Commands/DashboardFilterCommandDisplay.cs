#nullable enable
using AIGuiders.Platform.CommandPlane;

namespace DashSpec.Host.Commands;

/// <summary>CLI-facing labels for dash-ccl (no leading slashes).</summary>
internal static class DashboardFilterCommandDisplay
{
    public const string RootVerb = "select";
    public const string Prompt = ">";
    public const string EmptyPlaceholder = "filter · report · page";
    public static string VisiblePrefix => $"{Prompt} {RootVerb} ";
    public const string AcceptCompletionHint = "Ctrl+Space — выбрать";

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

    public static string FormatSuggestion(SlashCompletionItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.PickValue))
        {
            return item.PickValue;
        }

        if (!string.IsNullOrWhiteSpace(item.StepSegment))
        {
            return item.StepSegment;
        }

        return FormatCommand(item.InsertText.TrimEnd());
    }

    public static string FormatTreeBreadcrumb(string typedBody)
    {
        var text = typedBody.Trim();
        if (text.Length == 0)
        {
            return RootVerb;
        }

        if (!text.StartsWith(RootVerb, StringComparison.OrdinalIgnoreCase))
        {
            return $"{RootVerb} {text}";
        }

        return text;
    }

    static string FormatBreadcrumb(string breadcrumb)
    {
        if (string.IsNullOrWhiteSpace(breadcrumb))
        {
            return RootVerb;
        }

        var text = breadcrumb.TrimStart('/').Replace(" › ", " ");
        if (!text.StartsWith(RootVerb, StringComparison.OrdinalIgnoreCase))
        {
            text = $"{RootVerb} {text}";
        }

        return text;
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
