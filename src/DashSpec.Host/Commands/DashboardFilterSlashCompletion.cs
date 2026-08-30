#nullable enable

using AIGuiders.Platform.CommandPlane;

namespace DashSpec.Host.Commands;

/// <summary>DashSpec CCL — multi-root command tree with human labels (DASHSPEC-ADR-0043).</summary>
internal static class DashboardFilterSlashCompletion
{
    public static SlashCompletionResult GetResult(
        SlashCatalogIndex catalog,
        DashboardFilterContext context,
        string typedLine,
        ISlashPickerChoiceSource pickerSource)
    {
        var body = SanitizeLine(typedLine);
        if (TryBuildTreeChoiceResult(catalog, context, body, out var treeChoice))
        {
            return treeChoice;
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

    public static string NormalizeBody(string typedLine) => SanitizeLine(typedLine).TrimEnd();

    public static string ToCommandLine(string typedLine) => EnsureCommandRoot(NormalizeBody(typedLine));

    public static bool IsIncompleteRoot(string typedLine)
    {
        var body = NormalizeBody(typedLine);
        return body.Equals(FilterCommandPaths.RootVerb, StringComparison.OrdinalIgnoreCase)
               || body.Equals(ViewCommandPaths.RootVerb, StringComparison.OrdinalIgnoreCase);
    }

    static string EnsureCommandRoot(string body)
    {
        if (body.Length == 0)
        {
            return "";
        }

        if (body.StartsWith($"{FilterCommandPaths.RootVerb} ", StringComparison.OrdinalIgnoreCase)
            || body.Equals(FilterCommandPaths.RootVerb, StringComparison.OrdinalIgnoreCase)
            || body.StartsWith($"{ViewCommandPaths.RootVerb} ", StringComparison.OrdinalIgnoreCase)
            || body.Equals(ViewCommandPaths.RootVerb, StringComparison.OrdinalIgnoreCase))
        {
            return body;
        }

        if (body.StartsWith($"{FilterCommandPaths.FilterBranch} ", StringComparison.OrdinalIgnoreCase)
            || body.Equals(FilterCommandPaths.FilterBranch, StringComparison.OrdinalIgnoreCase)
            || body.StartsWith($"{FilterCommandPaths.ReportBranch} ", StringComparison.OrdinalIgnoreCase)
            || body.Equals(FilterCommandPaths.ReportBranch, StringComparison.OrdinalIgnoreCase)
            || body.StartsWith($"{FilterCommandPaths.PageBranch} ", StringComparison.OrdinalIgnoreCase)
            || body.Equals(FilterCommandPaths.PageBranch, StringComparison.OrdinalIgnoreCase))
        {
            return $"{FilterCommandPaths.RootVerb} {body}";
        }

        return body;
    }

    public static string ToInputTail(string line) => SanitizeLine(line);

    public static string LineFromInsert(string insertText) => SanitizeLine(insertText);

    static bool TryPeelDuplicateSelect(ref string text)
    {
        if (text.StartsWith($"{FilterCommandPaths.RootVerb} ", StringComparison.OrdinalIgnoreCase))
        {
            var rest = text[(FilterCommandPaths.RootVerb.Length + 1)..].TrimStart();
            if (rest.StartsWith($"{FilterCommandPaths.RootVerb} ", StringComparison.OrdinalIgnoreCase)
                || rest.StartsWith('/'))
            {
                text = rest.StartsWith('/') ? rest[1..].TrimStart() : rest;
                return true;
            }

            return false;
        }

        if (text.Equals(FilterCommandPaths.RootVerb, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (text.StartsWith(FilterCommandPaths.RootVerb, StringComparison.OrdinalIgnoreCase))
        {
            text = text[FilterCommandPaths.RootVerb.Length..].TrimStart();
            return true;
        }

        return false;
    }

    static bool TryBuildTreeChoiceResult(
        SlashCatalogIndex catalog,
        DashboardFilterContext context,
        string body,
        out SlashCompletionResult result)
    {
        result = default!;
        if (IsArgOrReadyPhase(catalog, context, body))
        {
            return false;
        }

        var parsed = ParseBody(body);
        if (parsed.Depth == 0)
        {
            return TryBuildVerbRootChoice(context, body, parsed.Partial, out result);
        }

        if (parsed.Root.Equals(FilterCommandPaths.RootVerb, StringComparison.OrdinalIgnoreCase))
        {
            if (parsed.Depth == 1)
            {
                return TryBuildSelectBranchChoice(context, body, parsed.Partial, out result);
            }

            if (parsed.Branch.Equals(FilterCommandPaths.ReportBranch, StringComparison.OrdinalIgnoreCase))
            {
                return TryBuildReportChoice(context, body, parsed.Partial, out result);
            }

            if (parsed.Branch.Equals(FilterCommandPaths.PageBranch, StringComparison.OrdinalIgnoreCase))
            {
                return TryBuildPageChoice(context, body, parsed.Partial, out result);
            }

            if (parsed.Branch.Equals(FilterCommandPaths.FilterBranch, StringComparison.OrdinalIgnoreCase))
            {
                return TryBuildFilterChoice(catalog, context, body, parsed.Partial, out result);
            }
        }

        if (parsed.Root.Equals(ViewCommandPaths.RootVerb, StringComparison.OrdinalIgnoreCase))
        {
            if (parsed.Depth == 1)
            {
                return TryBuildViewCardChoice(context, body, parsed.Partial, out result);
            }

            return TryBuildViewKindChoice(context, body, parsed.Branch, parsed.Partial, out result);
        }

        return false;
    }

    static bool TryBuildVerbRootChoice(
        DashboardFilterContext context,
        string body,
        string partial,
        out SlashCompletionResult result)
    {
        var items = new List<SlashCompletionItem>();
        if (MatchesPartial(FilterCommandPaths.RootVerb, partial))
        {
            items.Add(RootVerbItem(FilterCommandPaths.RootVerb, "Срез данных — filter, report, page"));
        }

        if (context.SwitchableCards.Count > 0 && MatchesPartial(ViewCommandPaths.RootVerb, partial))
        {
            items.Add(RootVerbItem(ViewCommandPaths.RootVerb, "Представление карточки — heatmap, line…"));
        }

        if (items.Count == 0)
        {
            result = default!;
            return false;
        }

        result = new SlashCompletionResult(
            items,
            TreeGuidance(body, DashboardFilterCommandDisplay.AcceptHint("команду"), "select · view"));
        return true;
    }

    static bool TryBuildSelectBranchChoice(
        DashboardFilterContext context,
        string body,
        string partial,
        out SlashCompletionResult result)
    {
        var items = new List<SlashCompletionItem>();
        if (MatchesPartial(FilterCommandPaths.FilterBranch, partial))
        {
            items.Add(SelectBranchItem(FilterCommandPaths.FilterBranch, "Фильтры toolbar — как в UI"));
        }

        if (context.CatalogEntries.Count > 0 && MatchesPartial(FilterCommandPaths.ReportBranch, partial))
        {
            items.Add(SelectBranchItem(FilterCommandPaths.ReportBranch, "Отчёты каталога"));
        }

        if (context.ReportPages.Count > 1 && MatchesPartial(FilterCommandPaths.PageBranch, partial))
        {
            items.Add(SelectBranchItem(FilterCommandPaths.PageBranch, "Страницы отчёта"));
        }

        if (items.Count == 0)
        {
            result = default!;
            return false;
        }

        result = new SlashCompletionResult(
            items,
            TreeGuidance(body, DashboardFilterCommandDisplay.AcceptHint("ветку"), "filter · report · page"));
        return true;
    }

    static bool TryBuildReportChoice(
        DashboardFilterContext context,
        string body,
        string partial,
        out SlashCompletionResult result)
    {
        var items = context.CatalogEntries
            .Where(entry => MatchesPartial(entry.Id, partial) || MatchesPartial(entry.Title, partial))
            .Select(entry => new SlashCompletionItem(
                $"select report {entry.Id} ",
                $"select report {entry.Id}",
                entry.Title,
                "Report",
                entry.Id))
            .ToList();

        if (items.Count == 0)
        {
            result = default!;
            return false;
        }

        result = new SlashCompletionResult(
            items,
            TreeGuidance(body, DashboardFilterCommandDisplay.AcceptHint("отчёт"), "название отчёта"));
        return true;
    }

    static bool TryBuildPageChoice(
        DashboardFilterContext context,
        string body,
        string partial,
        out SlashCompletionResult result)
    {
        var items = context.ReportPages
            .Where(page => MatchesPartial(page.Id, partial) || MatchesPartial(page.Title, partial))
            .Select(page => new SlashCompletionItem(
                $"select page {page.Id} ",
                $"select page {page.Id}",
                page.Title,
                "Report",
                page.Id))
            .ToList();

        if (items.Count == 0)
        {
            result = default!;
            return false;
        }

        result = new SlashCompletionResult(
            items,
            TreeGuidance(body, DashboardFilterCommandDisplay.AcceptHint("страницу"), "название страницы"));
        return true;
    }

    static bool TryBuildFilterChoice(
        SlashCatalogIndex catalog,
        DashboardFilterContext context,
        string body,
        string partial,
        out SlashCompletionResult result)
    {
        var items = new List<SlashCompletionItem>();
        foreach (var filterName in context.ToolbarFilterNames)
        {
            var label = DashboardFilterSlashLabels.ResolveFilterLabel(context, filterName);
            if (!MatchesPartial(filterName, partial) && !MatchesPartial(label, partial))
            {
                continue;
            }

            var path = FilterCommandPaths.FilterPath(filterName);
            if (!catalog.TryGet(path, out var route))
            {
                continue;
            }

            items.Add(new SlashCompletionItem(
                path + " ",
                route.SlashPath,
                route.Help,
                route.Group ?? "Filter",
                filterName));
        }

        if (items.Count == 0)
        {
            result = default!;
            return false;
        }

        result = new SlashCompletionResult(
            items,
            TreeGuidance(body, DashboardFilterCommandDisplay.AcceptHint("фильтр"), "название фильтра · значение"));
        return true;
    }

    static bool TryBuildViewCardChoice(
        DashboardFilterContext context,
        string body,
        string partial,
        out SlashCompletionResult result)
    {
        var items = context.SwitchableCards
            .Where(card => MatchesPartial(card.CardId, partial) || MatchesPartial(card.Title, partial))
            .Select(card => new SlashCompletionItem(
                ViewCommandPaths.CardPath(card.CardId) + " ",
                ViewCommandPaths.CardPath(card.CardId),
                card.Title,
                "View",
                card.CardId))
            .ToList();

        if (items.Count == 0)
        {
            result = default!;
            return false;
        }

        result = new SlashCompletionResult(
            items,
            TreeGuidance(body, DashboardFilterCommandDisplay.AcceptHint("карточку"), "название карточки"));
        return true;
    }

    static bool TryBuildViewKindChoice(
        DashboardFilterContext context,
        string body,
        string cardToken,
        string partial,
        out SlashCompletionResult result)
    {
        var card = DashboardCommandEntityResolver.ResolveCard(cardToken, context)
                   ?? context.SwitchableCards.FirstOrDefault(target =>
                       target.CardId.Equals(cardToken, StringComparison.OrdinalIgnoreCase));
        if (card is null)
        {
            result = default!;
            return false;
        }

        var items = card.Views
            .Where(view => MatchesPartial(view.ViewId, partial) || MatchesPartial(view.Label, partial))
            .Select(view => new SlashCompletionItem(
                ViewCommandPaths.ViewPath(card.CardId, view.ViewId) + " ",
                ViewCommandPaths.ViewPath(card.CardId, view.ViewId),
                view.Label,
                "View",
                view.ViewId))
            .ToList();

        if (items.Count == 0)
        {
            result = default!;
            return false;
        }

        result = new SlashCompletionResult(
            items,
            TreeGuidance(body, DashboardFilterCommandDisplay.AcceptHint("вид"), "heatmap · line · …"));
        return true;
    }

    static SlashCompletionItem RootVerbItem(string verb, string help) =>
        new($"{verb} ", verb, help, "Command", verb);

    static SlashCompletionItem SelectBranchItem(string branch, string help) =>
        new($"select {branch} ", $"select {branch}", help, "Command", branch);

    static SlashInputGuidance TreeGuidance(string body, string placeholder, string nextStepHint) =>
        new(
            SlashInputMode.Path,
            DashboardFilterCommandDisplay.FormatTreeBreadcrumb(body),
            placeholder,
            nextStepHint,
            null,
            nameof(SlashArgTailKind.None));

    static bool IsTreeBranchPath(string canonicalPath, DashboardFilterContext context)
    {
        if (canonicalPath.Equals($"select {FilterCommandPaths.FilterBranch}", StringComparison.OrdinalIgnoreCase)
            || canonicalPath.Equals($"select {FilterCommandPaths.ReportBranch}", StringComparison.OrdinalIgnoreCase)
            || canonicalPath.Equals($"select {FilterCommandPaths.PageBranch}", StringComparison.OrdinalIgnoreCase)
            || canonicalPath.Equals(ViewCommandPaths.RootVerb, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return context.SwitchableCards.Any(card =>
            canonicalPath.Equals(ViewCommandPaths.CardPath(card.CardId), StringComparison.OrdinalIgnoreCase));
    }

    static bool IsArgOrReadyPhase(SlashCatalogIndex catalog, DashboardFilterContext context, string body)
    {
        if (body.Length == 0)
        {
            return false;
        }

        if (!SlashLineResolver.TryResolveBody(body, catalog, out var line)
            || !catalog.TryGet(line.CanonicalPath, out var route))
        {
            return false;
        }

        if (IsTreeBranchPath(line.CanonicalPath, context) && !line.HasArgTailContent)
        {
            return false;
        }

        if (line.HasArgTailContent)
        {
            return true;
        }

        if (line.IsExactPathMatch
            && route.ArgTailKind is SlashArgTailKind.Picker or SlashArgTailKind.Required
            && (line.EndsWithSpaceAfterPath || body.EndsWith(' ')))
        {
            return true;
        }

        if (!line.IsExactPathMatch)
        {
            return false;
        }

        return line.EndsWithSpaceAfterPath || route.ArgTailKind == SlashArgTailKind.None;
    }

    static ParsedBody ParseBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return new("", "", "", 0);
        }

        var tokens = body.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 1)
        {
            return new(tokens[0], "", "", 1);
        }

        if (tokens.Length == 2)
        {
            return new(tokens[0], tokens[1], "", 2);
        }

        return new(tokens[0], tokens[1], tokens[2], 3);
    }

    readonly record struct ParsedBody(string Root, string Branch, string Partial, int Depth);

    static bool MatchesPartial(string value, string partial) =>
        partial.Length == 0 || value.StartsWith(partial, StringComparison.OrdinalIgnoreCase);
}
