#nullable enable
using AIGuiders.Platform.CommandPlane;

namespace DashSpec.Host.Commands;

/// <summary>DashSpec filter slash UX — flat filter list + platform arg pickers (DASHSPEC-ADR-0043).</summary>
internal static class DashboardFilterSlashCompletion
{
    public static SlashCompletionResult GetResult(
        SlashCatalogIndex catalog,
        DashboardFilterContext context,
        string typedBody,
        ISlashPickerChoiceSource pickerSource)
    {
        var body = NormalizeBody(typedBody);
        if (TryBuildFilterChoiceResult(catalog, context, body, out var filterChoice))
        {
            return filterChoice;
        }

        return SlashCompletion.GetResult(catalog, body, pickerSource);
    }

    public static string NormalizeBody(string typedBody)
    {
        var text = typedBody;
        if (text.StartsWith('/'))
        {
            text = text[1..];
        }

        text = text.TrimStart();
        if (text.Length == 0)
        {
            return "select";
        }

        if (!text.StartsWith("select", StringComparison.OrdinalIgnoreCase))
        {
            return "select " + text;
        }

        return text;
    }

    public static string ToSlashLine(string typedBody)
    {
        var body = NormalizeBody(typedBody);
        return body.Equals("select", StringComparison.OrdinalIgnoreCase)
            ? "/select"
            : "/" + body;
    }

    public static string TailFromInsert(string insertText)
    {
        var text = insertText.TrimStart('/').TrimStart();
        if (text.StartsWith("select ", StringComparison.OrdinalIgnoreCase))
        {
            return text["select ".Length..];
        }

        if (text.Equals("select", StringComparison.OrdinalIgnoreCase))
        {
            return "";
        }

        return text;
    }

    static bool TryBuildFilterChoiceResult(
        SlashCatalogIndex catalog,
        DashboardFilterContext context,
        string body,
        out SlashCompletionResult result)
    {
        result = default!;
        if (IsArgOrReadyPhase(catalog, body))
        {
            return false;
        }

        var partial = ExtractFilterPartial(body);
        var items = BuildFilterItems(catalog, context, partial);
        if (items.Count == 0)
        {
            return false;
        }

        var hint = items.Count == 1
            ? items[0].Help
            : $"{items.Count} фильтра на toolbar — Tab дополняет, Enter применяет после значения";

        result = new SlashCompletionResult(
            items,
            new SlashInputGuidance(
                SlashInputMode.Path,
                "/select",
                "Tab — выбрать фильтр",
                hint,
                null,
                nameof(SlashArgTailKind.None)));

        return true;
    }

    static bool IsArgOrReadyPhase(SlashCatalogIndex catalog, string body)
    {
        if (!SlashLineResolver.TryResolveBody(body, catalog, out var line)
            || !catalog.TryGet(line.CanonicalPath, out var route))
        {
            return false;
        }

        if (line.HasArgTailContent)
        {
            return true;
        }

        if (!line.IsExactPathMatch)
        {
            return false;
        }

        return line.EndsWithSpaceAfterPath || route.ArgTailKind == SlashArgTailKind.None;
    }

    static string ExtractFilterPartial(string body)
    {
        if (body.Length <= "select".Length)
        {
            return "";
        }

        var tail = body["select".Length..].TrimStart();
        if (tail.Length == 0)
        {
            return "";
        }

        var spaceIndex = tail.IndexOf(' ');
        return spaceIndex < 0 ? tail : tail[..spaceIndex];
    }

    static IReadOnlyList<SlashCompletionItem> BuildFilterItems(
        SlashCatalogIndex catalog,
        DashboardFilterContext context,
        string partial)
    {
        var items = new List<SlashCompletionItem>();

        if (DashboardCommandAliasResolver.ResolveDateFilter(context) is not null
            && MatchesPartial("date", partial)
            && catalog.TryGet("select date", out var dateRoute))
        {
            items.Add(new SlashCompletionItem(
                "/select date ",
                dateRoute.SlashPath,
                DashboardFilterSlashLabels.DateCommandHelp(context),
                dateRoute.Group ?? "Filters",
                "date"));
        }

        foreach (var alias in DashboardCommandAliasResolver.ResolveFieldSlashAliases(context))
        {
            if (!MatchesPartial(alias, partial))
            {
                continue;
            }

            var path = $"select {alias}";
            if (!catalog.TryGet(path, out var route))
            {
                continue;
            }

            items.Add(new SlashCompletionItem(
                $"/select {alias} ",
                route.SlashPath,
                DashboardFilterSlashLabels.FieldCommandHelp(context, alias),
                route.Group ?? "Filters",
                alias));
        }

        return items
            .OrderBy(item => item.StepSegment, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    static bool MatchesPartial(string value, string partial) =>
        partial.Length == 0
        || value.StartsWith(partial, StringComparison.OrdinalIgnoreCase);
}
