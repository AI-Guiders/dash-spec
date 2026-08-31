#nullable enable

using AIGuiders.Platform.CommandPlane;
using AIGuiders.Platform.CommandPlane.ArgSuggestions;
using AIGuiders.Platform.IntermediateRepresentation.Invocation;

namespace DashSpec.Host.Commands;

/// <summary>DashSpec CCL adapter — catalog path completion + display labels (DASHSPEC-ADR-0043).</summary>
internal static class DashboardFilterSlashCompletion
{
    public static SlashCompletionResult GetResult(
        CommandCatalogIndex catalog,
        DashboardFilterContext context,
        string typedLine,
        ICommandArgSuggestionBroker? suggestionBroker,
        ArgConstructorSession? constructorSession,
        SlashCompletionOptions? options = null)
    {
        var body = SanitizeLine(typedLine);
        var platform = SlashCompletion.GetResult(catalog, body, suggestionBroker, constructorSession, options);
        platform = EnrichPathGuidance(platform, body);
        return platform with { Guidance = DashboardFilterCommandDisplay.ForCli(platform.Guidance) };
    }

    public static bool TryResolveCommandPath(
        CommandCatalogIndex catalog,
        string typedLine,
        out string canonicalPath)
    {
        canonicalPath = "";
        var body = SanitizeLine(typedLine);
        if (!SlashLineResolver.TryResolveBody(body, catalog, out var line))
        {
            return false;
        }

        canonicalPath = line.CanonicalPath;
        return true;
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
               || body.Equals(ViewCommandPaths.RootVerb, StringComparison.OrdinalIgnoreCase)
               || body.Equals(ShowCommandPaths.RootVerb, StringComparison.OrdinalIgnoreCase);
    }

    public static string ToInputTail(string line) => SanitizeLine(line);

    public static bool TrySplitPathAndArg(
        CommandCatalogIndex catalog,
        string typedLine,
        out string canonicalPath,
        out string argTail)
    {
        canonicalPath = "";
        argTail = "";
        var body = SanitizeLine(typedLine);
        if (!SlashLineResolver.TryResolveBody(body, catalog, out var line))
        {
            return false;
        }

        canonicalPath = line.CanonicalPath;
        argTail = line.ArgTail;
        return true;
    }

    public static bool HasCommandPathChanged(
        CommandCatalogIndex catalog,
        string previousLine,
        string nextLine)
    {
        if (!TrySplitPathAndArg(catalog, previousLine, out var oldPath, out _)
            || !TrySplitPathAndArg(catalog, nextLine, out var newPath, out _))
        {
            return true;
        }

        return !string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase);
    }

    public static string LineFromInsert(string insertText) => SanitizeLine(insertText);

    static SlashCompletionResult EnrichPathGuidance(SlashCompletionResult result, string body)
    {
        if (result.Guidance.Phase != InvocationLinePhase.Path)
        {
            return result;
        }

        var hint = ResolvePathHint(body.Trim());
        if (hint is null)
        {
            return result;
        }

        return result with { Guidance = result.Guidance with { Hint = hint } };
    }

    static string? ResolvePathHint(string body) => body switch
    {
        "" => "select · view · show",
        _ when body.Equals(FilterCommandPaths.RootVerb, StringComparison.OrdinalIgnoreCase) =>
            "Срез данных — filter, report, page",
        _ when body.Equals($"select {FilterCommandPaths.FilterBranch}", StringComparison.OrdinalIgnoreCase)
               || body.StartsWith($"select {FilterCommandPaths.FilterBranch} ", StringComparison.OrdinalIgnoreCase) =>
            "название фильтра · значение",
        _ when body.Equals(ViewCommandPaths.RootVerb, StringComparison.OrdinalIgnoreCase) =>
            "Представление карточки — heatmap, line…",
        _ when body.Equals(ShowCommandPaths.RootVerb, StringComparison.OrdinalIgnoreCase)
               || body.StartsWith($"{ShowCommandPaths.RootVerb} ", StringComparison.OrdinalIgnoreCase) =>
            "dashboard · controlcenter",
        _ => null,
    };

    static string EnsureCommandRoot(string body)
    {
        if (body.Length == 0)
        {
            return "";
        }

        if (body.StartsWith($"{FilterCommandPaths.RootVerb} ", StringComparison.OrdinalIgnoreCase)
            || body.Equals(FilterCommandPaths.RootVerb, StringComparison.OrdinalIgnoreCase)
            || body.StartsWith($"{ViewCommandPaths.RootVerb} ", StringComparison.OrdinalIgnoreCase)
            || body.Equals(ViewCommandPaths.RootVerb, StringComparison.OrdinalIgnoreCase)
            || body.StartsWith($"{ShowCommandPaths.RootVerb} ", StringComparison.OrdinalIgnoreCase)
            || body.Equals(ShowCommandPaths.RootVerb, StringComparison.OrdinalIgnoreCase))
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
}
