#nullable enable

using AIGuiders.Platform.CommandPlane;

using DashSpec.Core.Model;



namespace DashSpec.Host.Commands;



/// <summary>DashSpec CCL — select report | page | filter tree (DASHSPEC-ADR-0043).</summary>

internal static class DashboardFilterSlashCompletion

{

    public static SlashCompletionResult GetResult(

        SlashCatalogIndex catalog,

        DashboardFilterContext context,

        string typedLine,

        ISlashPickerChoiceSource pickerSource)

    {

        var body = NormalizeBody(typedLine);

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



    public static string ToCommandLine(string typedLine) => NormalizeBody(typedLine);



    public static string LineFromInsert(string insertText) => SanitizeLine(insertText);



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



    static bool TryBuildTreeChoiceResult(

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



        var parsed = ParseBody(body);

        if (parsed.Depth == 0)
        {
            return TryBuildRootChoice(context, body, parsed.Partial, out result);
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



        return false;

    }



    static bool TryBuildRootChoice(
        DashboardFilterContext context,
        string body,
        string partial,
        out SlashCompletionResult result)

    {

        var items = new List<SlashCompletionItem>();



        if (MatchesPartial(FilterCommandPaths.FilterBranch, partial))

        {

            items.Add(BranchItem(FilterCommandPaths.FilterBranch, "Фильтры toolbar — как в UI"));

        }



        if (context.CatalogEntries.Count > 0 && MatchesPartial(FilterCommandPaths.ReportBranch, partial))

        {

            items.Add(BranchItem(FilterCommandPaths.ReportBranch, "Отчёты каталога"));

        }



        if (context.ReportPages.Count > 1 && MatchesPartial(FilterCommandPaths.PageBranch, partial))

        {

            items.Add(BranchItem(FilterCommandPaths.PageBranch, "Страницы отчёта"));

        }



        if (items.Count == 0)

        {

            result = default!;

            return false;

        }



        result = new SlashCompletionResult(
            items,
            TreeGuidance(body, DashboardFilterCommandDisplay.AcceptHint("выбрать ветку"), "filter · report · page"));
        return true;
    }

    static bool TryBuildReportChoice(
        DashboardFilterContext context,
        string body,
        string partial,
        out SlashCompletionResult result)

    {

        var items = context.CatalogEntries

            .Where(entry => MatchesPartial(entry.Id, partial)

                              || MatchesPartial(entry.Title, partial))

            .Select(entry => new SlashCompletionItem(

                $"select report {entry.Id}",

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
            TreeGuidance(body, DashboardFilterCommandDisplay.AcceptHint("отчёт"), "<id>"));
        return true;
    }

    static bool TryBuildPageChoice(
        DashboardFilterContext context,
        string body,
        string partial,
        out SlashCompletionResult result)

    {

        var items = context.ReportPages

            .Where(page => MatchesPartial(page.Id, partial)

                             || MatchesPartial(page.Title, partial))

            .Select(page => new SlashCompletionItem(

                $"select page {page.Id}",

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
            TreeGuidance(body, DashboardFilterCommandDisplay.AcceptHint("страницу"), "<id>"));
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

            if (!MatchesPartial(filterName, partial)

                && !MatchesPartial(DashboardFilterSlashLabels.ResolveFilterLabel(context, filterName), partial))

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
            TreeGuidance(body, DashboardFilterCommandDisplay.AcceptHint("фильтр"), "<id> · <value>"));
        return true;
    }

    static SlashCompletionItem BranchItem(string branch, string help) =>
        new($"select {branch} ", $"select {branch}", help, "Command", branch);

    static SlashInputGuidance TreeGuidance(string body, string placeholder, string nextStepHint) =>
        new(
            SlashInputMode.Path,
            DashboardFilterCommandDisplay.FormatTreeBreadcrumb(body),
            placeholder,
            nextStepHint,
            null,
            nameof(SlashArgTailKind.None));



    static bool IsTreeBranchPath(string canonicalPath) =>
        canonicalPath.Equals($"select {FilterCommandPaths.FilterBranch}", StringComparison.OrdinalIgnoreCase)
        || canonicalPath.Equals($"select {FilterCommandPaths.ReportBranch}", StringComparison.OrdinalIgnoreCase)
        || canonicalPath.Equals($"select {FilterCommandPaths.PageBranch}", StringComparison.OrdinalIgnoreCase);



    static bool IsArgOrReadyPhase(SlashCatalogIndex catalog, string body)

    {

        if (!SlashLineResolver.TryResolveBody(body, catalog, out var line)

            || !catalog.TryGet(line.CanonicalPath, out var route))

        {

            return false;

        }



        if (IsTreeBranchPath(line.CanonicalPath) && !line.HasArgTailContent)

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



    static ParsedBody ParseBody(string body)

    {

        if (body.Length <= DashboardFilterCommandDisplay.RootVerb.Length)

        {

            return new ParsedBody("", "", 0);

        }



        var tail = body[DashboardFilterCommandDisplay.RootVerb.Length..].TrimStart();

        if (tail.Length == 0)

        {

            return new ParsedBody("", "", 0);

        }



        var firstSpace = tail.IndexOf(' ');

        if (firstSpace < 0)

        {

            return new ParsedBody(tail, "", 1);

        }



        var branch = tail[..firstSpace];

        var rest = tail[(firstSpace + 1)..];

        if (rest.Trim().Length == 0)

        {

            return new ParsedBody(branch, "", 1);

        }



        var argSpace = rest.IndexOf(' ');

        var partial = argSpace < 0 ? rest.TrimEnd() : rest[..argSpace];

        return new ParsedBody(branch, partial, 2);

    }



    readonly record struct ParsedBody(string Branch, string Partial, int Depth);



    static bool MatchesPartial(string value, string partial) =>

        partial.Length == 0

        || value.StartsWith(partial, StringComparison.OrdinalIgnoreCase);

}

