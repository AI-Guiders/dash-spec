#nullable enable
using AIGuiders.Platform.CommandPlane;

namespace DashSpec.Host.Commands;

/// <summary>DashSpec filter command UX — CLI line + platform slash catalog (DASHSPEC-ADR-0043).</summary>
internal static class DashboardFilterSlashCompletion
{
    public static SlashCompletionResult GetResult(
        SlashCatalogIndex catalog,
        DashboardFilterContext context,
        string typedLine,
        ISlashPickerChoiceSource pickerSource)
    {
        var body = NormalizeBody(typedLine);
        if (TryBuildFilterChoiceResult(catalog, context, body, out var filterChoice))
        {
            return filterChoice;
        }

        var platform = SlashCompletion.GetResult(catalog, body, pickerSource);
        return platform with { Guidance = DashboardFilterCommandDisplay.ForCli(platform.Guidance) };
    }

    public static string SanitizeLine(string line)
    {
        var text = line.TrimStart();
        if (text.StartsWith('>'))
        {
            text = text[1..].TrimStart();
        }

        if (text.StartsWith('/'))
        {
            text = text[1..].TrimStart();
        }

        while (TryPeelDuplicateSelect(ref text))
        {
        }

        return text;
    }

    public static string NormalizeBody(string typedLine)
    {
        var text = SanitizeLine(typedLine);
        if (text.Length == 0)
        {
            return DashboardFilterCommandDisplay.RootVerb;
        }

        if (!text.StartsWith(DashboardFilterCommandDisplay.RootVerb, StringComparison.OrdinalIgnoreCase))
        {
            return $"{DashboardFilterCommandDisplay.RootVerb} {text}";
        }

        return text;
    }

    public static string ToCommandLine(string typedLine)
    {
        var body = NormalizeBody(typedLine);
        return body.Equals(DashboardFilterCommandDisplay.RootVerb, StringComparison.OrdinalIgnoreCase)
            ? DashboardFilterCommandDisplay.RootVerb
            : body;
    }

    public static string LineFromInsert(string insertText) =>
        SanitizeLine(insertText);

    static bool TryPeelDuplicateSelect(ref string text)
    {
        if (text.StartsWith($"{DashboardFilterCommandDisplay.RootVerb} ", StringComparison.OrdinalIgnoreCase))
        {
            var rest = text[(DashboardFilterCommandDisplay.RootVerb.Length + 1)..].TrimStart();
            if (rest.StartsWith($"{DashboardFilterCommandDisplay.RootVerb} ", StringComparison.OrdinalIgnoreCase)
                || rest.StartsWith('/'))
            {
                text = rest.StartsWith('/') ? rest[1..].TrimStart() : rest;
                return true;
            }

            return false;
        }

        if (text.Equals(DashboardFilterCommandDisplay.RootVerb, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (text.StartsWith(DashboardFilterCommandDisplay.RootVerb, StringComparison.OrdinalIgnoreCase))
        {
            text = text[DashboardFilterCommandDisplay.RootVerb.Length..].TrimStart();
            return true;
        }

        return false;
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
            : $"{items.Count} фильтров — Tab выбрать, Enter после значения";

        result = new SlashCompletionResult(
            items,
            new SlashInputGuidance(
                SlashInputMode.Path,
                DashboardFilterCommandDisplay.RootVerb,
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
        if (body.Length <= DashboardFilterCommandDisplay.RootVerb.Length)
        {
            return "";
        }

        var tail = body[DashboardFilterCommandDisplay.RootVerb.Length..].TrimStart();
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
                "select date ",
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
                $"select {alias} ",
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
