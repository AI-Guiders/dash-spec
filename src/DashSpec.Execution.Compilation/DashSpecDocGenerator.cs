using System.Text;
using AIGuiders.Platform.Modeling.Paths;
using DashSpec.Modeling.Core;
using DashSpec.Modeling.Parse.Document;
using DashSpec.Modeling.Parse.Syntax;
using DashSpec.Modeling.Parse.Texts;
using Microsoft.FSharp.Core;

namespace DashSpec.Execution.Compilation;

public sealed record DashSpecDocSection(
    string CategoryId,
    string CategoryTitle,
    IReadOnlyList<DashSpecSymbolDoc> Units);

public sealed record DashSpecDocCatalog(
    string Title,
    string Language,
    IReadOnlyList<DashSpecDocSection> Sections,
    IReadOnlyDictionary<string, DashSpecSymbolDoc> SymbolIndex);

public sealed record DashSpecCompiledBookPage(
    string Id,
    string Title,
    string Markdown,
    string? SymbolsCategory,
    string? EmbeddedReportUnit,
    IReadOnlyList<DashSpecCompiledBookPage> SubSections,
    IReadOnlyList<DashSpecDocSection> SymbolSections);

public sealed record DashSpecCompiledBook(
    string Id,
    string Title,
    string Markdown,
    string Language,
    IReadOnlyList<DashSpecCompiledBookPage> Pages,
    IReadOnlyDictionary<string, DashSpecSymbolDoc> GlobalSymbolIndex);

/// <summary>
/// Self-documenting language &amp; workspace documentation generator (DASHSPEC-ADR-0138).
/// Builds structured documentation catalogs and Markdown reference books directly from
/// <c>///</c> doc comments, <c>DashSpecGrammarGraph</c> grammar projections, and <c>module texts</c>
/// localization tables across <c>stdlib</c> and user <c>.ds</c> workspaces.
/// </summary>
public static class DashSpecDocGenerator
{
    public static DashSpecDocCatalog BuildStdlibCatalog(string? language = null)
    {
        DashSpec.Execution.Parsing.DashSpecParser.EnsureStdlibRegistered();
        UnitImport.ensureBuiltinUnitsRegistered();
        var lang = NormalizeLanguage(language);
        var state = CreateStdlibTextState(lang);
        FSharpFunc<string, FSharpOption<string>> resolver = FuncConvert.FromFunc<string, FSharpOption<string>>(
            state.TryResolveRawText);

        var unitDocs = new List<DashSpecSymbolDoc>();
        var transformDocs = new List<DashSpecSymbolDoc>();
        var presentationDocs = new List<DashSpecSymbolDoc>();
        var typeDocs = new List<DashSpecSymbolDoc>();
        var ruleDocs = new List<DashSpecSymbolDoc>();
        var monitoringDocs = new List<DashSpecSymbolDoc>();

        foreach (var resName in UnitSource.embeddedUnitPaths().OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            if (!resName.StartsWith("stdlib/", StringComparison.OrdinalIgnoreCase) ||
                !resName.EndsWith(".ds", StringComparison.OrdinalIgnoreCase) ||
                resName.StartsWith("stdlib/texts/", StringComparison.OrdinalIgnoreCase) ||
                resName.StartsWith("stdlib/documentation/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var textOpt = UnitSource.tryReadRaw(resName);
            if (FSharpOption<string>.get_IsNone(textOpt))
            {
                continue;
            }

            var text = textOpt.Value;
            var extracted = DashSpecDocumentationExtractor.extractFromText(
                FSharpOption<string>.Some(resName),
                resolver,
                text);

            foreach (var doc in extracted)
            {
                if (resName.StartsWith("stdlib/units/", StringComparison.OrdinalIgnoreCase))
                {
                    // Promote each language unit row from `module registry units` to a first-class navigable symbol
                    var promotedUnits = doc.Children
                        .Where(c => string.Equals(c.Kind, "unit", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (promotedUnits.Count > 0)
                    {
                        unitDocs.AddRange(promotedUnits);
                    }
                    else
                    {
                        unitDocs.Add(doc);
                    }
                }
                else if (resName.StartsWith("stdlib/transforms/", StringComparison.OrdinalIgnoreCase))
                {
                    transformDocs.Add(doc);
                }
                else if (resName.StartsWith("stdlib/presentation/", StringComparison.OrdinalIgnoreCase))
                {
                    presentationDocs.Add(doc);
                }
                else if (resName.StartsWith("stdlib/types/", StringComparison.OrdinalIgnoreCase))
                {
                    // Promote individual `type <Name>` blocks to first-class navigable symbols
                    var promotedTypes = doc.Children
                        .Where(c => string.Equals(c.Kind, "type", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (promotedTypes.Count > 0)
                    {
                        typeDocs.AddRange(promotedTypes);
                    }
                    else
                    {
                        typeDocs.Add(doc);
                    }
                }
                else if (resName.StartsWith("stdlib/rules/", StringComparison.OrdinalIgnoreCase))
                {
                    // Promote each compiler rule from `module registry rules` to a first-class navigable symbol
                    var promotedRules = doc.Children
                        .Where(c => string.Equals(c.Kind, "rule", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (promotedRules.Count > 0)
                    {
                        ruleDocs.AddRange(promotedRules);
                    }
                    else
                    {
                        ruleDocs.Add(doc);
                    }
                }
                else if (resName.StartsWith("stdlib/monitoring/", StringComparison.OrdinalIgnoreCase))
                {
                    monitoringDocs.Add(doc);
                }
            }
        }

        var isRu = lang.StartsWith("ru", StringComparison.OrdinalIgnoreCase);
        var sections = new List<DashSpecDocSection>
        {
            new("units", isRu ? "Модули языка (stdlib/units)" : "Language Units (stdlib/units)", unitDocs),
            new("transforms", isRu ? "Трансформации потоков данных (stdlib/transforms)" : "Dataflow Transforms (stdlib/transforms)", transformDocs),
            new("presentations", isRu ? "Стандартные пресеты оформления (stdlib/presentation)" : "Presentation Presets (stdlib/presentation)", presentationDocs),
            new("types", isRu ? "Стандартные типы данных (stdlib/types)" : "Standard Types (stdlib/types)", typeDocs),
            new("monitoring", isRu ? "Самодиагностика и мониторинг (stdlib/monitoring)" : "Self-Monitoring (stdlib/monitoring)", monitoringDocs),
            new("rules", isRu ? "Правила и диагностики компилятора (stdlib/rules)" : "Compiler Rules & Diagnostics (stdlib/rules)", ruleDocs),
        };

        var allDocs = sections.SelectMany(s => s.Units).ToArray();
        var index = DashSpecDocumentationExtractor.indexBySymbol(allDocs);

        return new DashSpecDocCatalog(
            isRu ? "Справочник стандартной библиотеки DashSpec (stdlib)" : "DashSpec Standard Library Reference (stdlib)",
            lang,
            sections,
            index);
    }

    public static DashSpecDocCatalog BuildWorkspaceCatalog(string workspaceDir, string? language = null)
    {
        UnitImport.ensureBuiltinUnitsRegistered();
        var lang = NormalizeLanguage(language);
        var isRu = lang.StartsWith("ru", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(workspaceDir) || !Directory.Exists(workspaceDir))
        {
            return new DashSpecDocCatalog(
                isRu ? "Каталог спецификаций" : "Workspace Specification Catalog",
                lang,
                [],
                new Dictionary<string, DashSpecSymbolDoc>(StringComparer.OrdinalIgnoreCase));
        }

        var fullRoot = Path.GetFullPath(workspaceDir);
        var files = UnitIndexModule.corpusFiles(fullRoot)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var parsedUnits = new List<(string FilePath, string RawText, ParseTree Tree, FSharpOption<DashSpec.Modeling.Parse.UnitGrammar.UnitEnvelope> Envelope)>();
        var state = CreateStdlibTextState(lang);
        foreach (var file in files)
        {
            try
            {
                var rawOpt = UnitSource.tryReadRaw(file);
                if (FSharpOption<string>.get_IsNone(rawOpt))
                {
                    continue;
                }

                var raw = rawOpt.Value;
                var tree = SyntaxTree.parse(raw);
                var envOpt = DashSpec.Modeling.Parse.UnitGrammar.tryUnwrapAny(tree.Root);
                parsedUnits.Add((file, raw, tree, envOpt));

                if (FSharpOption<DashSpec.Modeling.Parse.UnitGrammar.UnitEnvelope>.get_IsSome(envOpt) &&
                    envOpt.Value.Kind.IsTexts)
                {
                    var doc = TextsModuleParser.parseEnvelope(envOpt.Value);
                    var docLang = FSharpOption<string>.get_IsSome(doc.Language) ? doc.Language.Value : "";
                    if (string.IsNullOrWhiteSpace(docLang) ||
                        docLang.StartsWith(lang[..2], StringComparison.OrdinalIgnoreCase))
                    {
                        state.RegisterTexts(doc);
                    }
                }
            }
            catch
            {
                // Ignore incomplete draft files during doc scan
            }
        }

        FSharpFunc<string, FSharpOption<string>> resolver = FuncConvert.FromFunc<string, FSharpOption<string>>(
            state.TryResolveRawText);

        var byKind = new Dictionary<string, List<DashSpecSymbolDoc>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (file, _, tree, _) in parsedUnits)
        {
            try
            {
                var relPath = LogicalPath.Create(Path.GetRelativePath(fullRoot, file)).Value;
                var extracted = DashSpecDocumentationExtractor.extractFromTree(
                    FSharpOption<string>.Some(relPath),
                    resolver,
                    tree);
                foreach (var doc in extracted)
                {
                    if (!byKind.TryGetValue(doc.Kind, out var list))
                    {
                        list = [];
                        byKind[doc.Kind] = list;
                    }
                    list.Add(doc);
                }
            }
            catch
            {
                // Ignore unparseable file
            }
        }

        var sections = byKind
            .OrderBy(kv => SectionOrder(kv.Key))
            .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => new DashSpecDocSection(
                kv.Key,
                FormatWorkspaceKindTitle(kv.Key, isRu),
                kv.Value))
            .ToList();

        var allDocs = sections.SelectMany(s => s.Units).ToArray();
        var index = DashSpecDocumentationExtractor.indexBySymbol(allDocs);

        return new DashSpecDocCatalog(
            isRu ? $"Документация каталога ({Path.GetFileName(fullRoot)})" : $"Workspace Documentation ({Path.GetFileName(fullRoot)})",
            lang,
            sections,
            index);
    }

    public static DashSpecCompiledBook BuildDocumentationBook(string? workspaceDir = null, string? language = null)
    {
        var lang = NormalizeLanguage(language);
        var stdlib = BuildStdlibCatalog(lang);
        var workspace = BuildWorkspaceCatalog(workspaceDir ?? "", lang);

        var globalIndex = new Dictionary<string, DashSpecSymbolDoc>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in stdlib.SymbolIndex)
        {
            globalIndex[kv.Key] = kv.Value;
        }
        foreach (var kv in workspace.SymbolIndex)
        {
            globalIndex[kv.Key] = kv.Value;
        }

        string? docModuleText = null;
        if (!string.IsNullOrWhiteSpace(workspaceDir) && Directory.Exists(workspaceDir))
        {
            foreach (var file in UnitIndexModule.corpusFiles(Path.GetFullPath(workspaceDir)))
            {
                var rawOpt = UnitSource.tryReadRaw(file);
                if (FSharpOption<string>.get_IsSome(rawOpt))
                {
                    var (kind, _) = UnitSource.tryKindAndId(rawOpt.Value);
                    if (string.Equals(kind, "documentation", StringComparison.OrdinalIgnoreCase))
                    {
                        docModuleText = rawOpt.Value;
                        break;
                    }
                }
            }
        }

        if (string.IsNullOrWhiteSpace(docModuleText))
        {
            var manualOpt = UnitSource.tryReadRaw("stdlib/documentation/dashspec_manual.ds");
            if (FSharpOption<string>.get_IsSome(manualOpt))
            {
                docModuleText = manualOpt.Value;
            }
        }

        if (string.IsNullOrWhiteSpace(docModuleText))
        {
            return new DashSpecCompiledBook(
                "dashspec_manual",
                stdlib.Title,
                "",
                lang,
                [],
                globalIndex);
        }

        var parsed = DocumentationModuleParser.parse(docModuleText);
        var isRu = lang.StartsWith("ru", StringComparison.OrdinalIgnoreCase);
        var compiledPages = parsed.Pages
            .Select(p => CompileBookPage(p, stdlib, workspace, isRu))
            .ToList();

        return new DashSpecCompiledBook(
            parsed.Id,
            parsed.Title,
            parsed.Markdown,
            lang,
            compiledPages,
            globalIndex);
    }

    private static DashSpecCompiledBookPage CompileBookPage(
        DocumentationPageDefinition page,
        DashSpecDocCatalog stdlib,
        DashSpecDocCatalog workspace,
        bool isRu)
    {
        var catOpt = FSharpOption<DocumentationSymbolsCategory>.get_IsSome(page.SymbolsCategory)
            ? page.SymbolsCategory.Value
            : null;
        var catId = catOpt is not null
            ? DocumentationSymbolsCategoryModule.toCategoryId(catOpt)
            : null;
        var reportUnit = FSharpOption<string>.get_IsSome(page.EmbeddedReportUnit)
            ? page.EmbeddedReportUnit.Value.Trim()
            : null;

        IReadOnlyList<DashSpecDocSection> rawSections = catOpt switch
        {
            { IsWorkspace: true } => workspace.Sections,
            { IsAll: true } => stdlib.Sections.Concat(workspace.Sections).ToList(),
            not null => stdlib.Sections
                .Where(s => string.Equals(s.CategoryId, catId, StringComparison.OrdinalIgnoreCase))
                .ToList(),
            _ => [],
        };

        var filteredSections = FilterAndGroupSections(page, rawSections, isRu);

        var subSections = page.Sections
            .Select(s => CompileBookPage(s, stdlib, workspace, isRu))
            .ToList();

        return new DashSpecCompiledBookPage(
            page.Id,
            page.Title,
            page.Markdown,
            catId,
            reportUnit,
            subSections,
            filteredSections);
    }

    private static IReadOnlyList<DashSpecDocSection> FilterAndGroupSections(
        DocumentationPageDefinition page,
        IReadOnlyList<DashSpecDocSection> rawSections,
        bool isRu)
    {
        if (rawSections.Count == 0)
        {
            return [];
        }

        var roleFilter = FSharpOption<string>.get_IsSome(page.RoleFilter) ? page.RoleFilter.Value : null;
        var localityFilter = FSharpOption<string>.get_IsSome(page.LocalityFilter) ? page.LocalityFilter.Value : null;
        var severityFilter = FSharpOption<string>.get_IsSome(page.SeverityFilter) ? page.SeverityFilter.Value : null;
        var kindFilter = FSharpOption<string>.get_IsSome(page.KindFilter) ? page.KindFilter.Value : null;
        var nsFilter = FSharpOption<string>.get_IsSome(page.NamespaceFilter) ? page.NamespaceFilter.Value : null;
        var groupByOpt = FSharpOption<DocumentationGroupBy>.get_IsSome(page.GroupBy) ? page.GroupBy.Value : null;

        bool MatchesFilters(DashSpecSymbolDoc doc)
        {
            if (!string.IsNullOrWhiteSpace(roleFilter))
            {
                var role = TryGetSymbolTag(doc, "role");
                if (!string.Equals(role, roleFilter, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(localityFilter))
            {
                var loc = TryGetSymbolTag(doc, "locality");
                if (!string.Equals(loc, localityFilter, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(severityFilter))
            {
                var sev = TryGetSymbolTag(doc, "severity");
                if (!string.Equals(sev, severityFilter, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(kindFilter))
            {
                var normKind = doc.Kind.StartsWith("module ", StringComparison.OrdinalIgnoreCase)
                    ? doc.Kind[7..].Trim()
                    : doc.Kind;
                if (!string.Equals(doc.Kind, kindFilter, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(normKind, kindFilter, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(nsFilter))
            {
                var ns = FSharpOption<string>.get_IsSome(doc.Namespace) ? doc.Namespace.Value : "";
                if (!ns.StartsWith(nsFilter, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        if (groupByOpt is null || groupByOpt.IsNoGrouping)
        {
            var result = new List<DashSpecDocSection>();
            foreach (var sec in rawSections)
            {
                var matching = sec.Units.Where(MatchesFilters).ToList();
                if (matching.Count > 0)
                {
                    result.Add(new DashSpecDocSection(sec.CategoryId, sec.CategoryTitle, matching));
                }
            }
            return result;
        }

        var allMatching = rawSections
            .SelectMany(s => s.Units)
            .Where(MatchesFilters)
            .ToList();

        if (allMatching.Count == 0)
        {
            return [];
        }

        string GroupKey(DashSpecSymbolDoc doc) =>
            groupByOpt switch
            {
                { IsRole: true } => TryGetSymbolTag(doc, "role") ?? "other",
                { IsLocality: true } => TryGetSymbolTag(doc, "locality") ?? "other",
                { IsSeverity: true } => TryGetSymbolTag(doc, "severity") ?? "other",
                { IsKind: true } => doc.Kind,
                { IsNamespace: true } => FSharpOption<string>.get_IsSome(doc.Namespace) ? doc.Namespace.Value : "global",
                _ => "all",
            };

        int GroupOrder(string key) =>
            (groupByOpt, key.ToLowerInvariant()) switch
            {
                ({ IsRole: true }, "declaration") => 0,
                ({ IsRole: true }, "consumer") => 1,
                ({ IsRole: true }, "root") => 2,
                ({ IsLocality: true }, "database") => 0,
                ({ IsLocality: true }, "runtime") => 1,
                ({ IsSeverity: true }, "error") => 0,
                ({ IsSeverity: true }, "warning") => 1,
                ({ IsSeverity: true }, "info") => 2,
                ({ IsKind: true }, var k) => SectionOrder(k),
                _ => 10,
            };

        string GroupTitle(string key) =>
            (groupByOpt, key.ToLowerInvariant(), isRu) switch
            {
                ({ IsRole: true }, "declaration", true) => "Декларативные модули (role = declaration)",
                ({ IsRole: true }, "declaration", false) => "Declaration Modules (role = declaration)",
                ({ IsRole: true }, "consumer", true) => "Потребляющие и компонующие модули (role = consumer)",
                ({ IsRole: true }, "consumer", false) => "Consumer & Composition Modules (role = consumer)",
                ({ IsRole: true }, "root", true) => "Корневые точки входа (role = root)",
                ({ IsRole: true }, "root", false) => "Root Entry Modules (role = root)",
                ({ IsLocality: true }, "database", true) => "Реляционные SQL-трансформации на стороне БД (locality = database)",
                ({ IsLocality: true }, "database", false) => "Database SQL Transforms (locality = database)",
                ({ IsLocality: true }, "runtime", true) => "Трансформации в памяти хоста (locality = runtime)",
                ({ IsLocality: true }, "runtime", false) => "In-Memory Runtime Transforms (locality = runtime)",
                ({ IsSeverity: true }, "error", true) => "Ошибки компиляции (severity = error)",
                ({ IsSeverity: true }, "error", false) => "Compiler Errors (severity = error)",
                ({ IsSeverity: true }, "warning", true) => "Предупреждения анализатора (severity = warning)",
                ({ IsSeverity: true }, "warning", false) => "Static Analysis Warnings (severity = warning)",
                ({ IsSeverity: true }, "info", true) => "Информационные рекомендации (severity = info)",
                ({ IsSeverity: true }, "info", false) => "Informational Hints (severity = info)",
                ({ IsKind: true }, _, _) => FormatWorkspaceKindTitle(key, isRu),
                _ => key,
            };

        return allMatching
            .GroupBy(GroupKey, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => GroupOrder(g.Key))
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new DashSpecDocSection(
                g.Key,
                GroupTitle(g.Key),
                g.ToList()))
            .ToList();
    }

    public static string? TryGetSymbolTag(DashSpecSymbolDoc doc, string key)
    {
        var opt = DashSpecSymbolDocModule.tryGetTag(key, doc);
        return FSharpOption<string>.get_IsSome(opt) ? opt.Value : null;
    }

    public static string RenderMarkdown(DashSpecDocCatalog catalog)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {catalog.Title}");
        sb.AppendLine();

        foreach (var section in catalog.Sections)
        {
            if (section.Units.Count == 0)
            {
                continue;
            }

            sb.AppendLine($"## {section.CategoryTitle}");
            sb.AppendLine();

            foreach (var unit in section.Units)
            {
                var titleSuffix = FSharpOption<string>.get_IsSome(unit.Title) && !string.IsNullOrWhiteSpace(unit.Title.Value)
                    ? $" — {unit.Title.Value}"
                    : "";
                sb.AppendLine($"### `{unit.Signature}`{titleSuffix}");
                if (FSharpOption<string>.get_IsSome(unit.SourcePath))
                {
                    sb.AppendLine($"*Источник:* `{unit.SourcePath.Value}:{unit.Line}`");
                }
                sb.AppendLine();

                if (!string.IsNullOrWhiteSpace(unit.Markdown))
                {
                    sb.AppendLine(unit.Markdown);
                    sb.AppendLine();
                }

                if (unit.Children.Length > 0)
                {
                    RenderChildrenMarkdown(sb, unit.Children, depth: 0);
                    sb.AppendLine();
                }
            }
        }

        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    private static void RenderChildrenMarkdown(StringBuilder sb, DashSpecSymbolDoc[] children, int depth)
    {
        var indent = new string(' ', depth * 2);
        foreach (var child in children)
        {
            var titlePart = FSharpOption<string>.get_IsSome(child.Title) && !string.IsNullOrWhiteSpace(child.Title.Value)
                ? $" (*{child.Title.Value}*)"
                : "";
            var docPart = !string.IsNullOrWhiteSpace(child.Markdown)
                ? $" — {child.Markdown.Replace("\n", " ")}"
                : "";
            sb.AppendLine($"{indent}- **`{child.Signature}`**{titlePart}{docPart}");
            if (child.Children.Length > 0)
            {
                RenderChildrenMarkdown(sb, child.Children, depth + 1);
            }
        }
    }

    private static DashSpec.Modeling.Parse.Import.ModuleImportState CreateStdlibTextState(string language)
    {
        DashSpec.Execution.Parsing.DashSpecParser.EnsureStdlibRegistered();
        var state = new DashSpec.Modeling.Parse.Import.ModuleImportState();
        var isRu = language.StartsWith("ru", StringComparison.OrdinalIgnoreCase);
        var preferredRes = isRu ? "stdlib/texts/rules.ru.ds" : "stdlib/texts/rules.en.ds";

        foreach (var resName in new[] { "stdlib/texts/rules.en.ds", preferredRes }.Distinct())
        {
            var textOpt = UnitSource.tryReadRaw(resName);
            if (FSharpOption<string>.get_IsNone(textOpt))
            {
                continue;
            }

            try
            {
                state.RegisterTexts(TextsModuleParser.parse(textOpt.Value));
            }
            catch
            {
                // Ignore parse issues
            }
        }

        return state;
    }

    private static string NormalizeLanguage(string? language) =>
        string.IsNullOrWhiteSpace(language) ? "ru-RU" : language.Trim();

    private static int SectionOrder(string kind) =>
        kind.ToLowerInvariant() switch
        {
            "module host" => 0,
            "module catalog" => 1,
            "module dashboard" => 2,
            "module section" => 3,
            "module types" => 4,
            "module flow" => 5,
            "module diagram" => 6,
            "module layout" => 7,
            "module presentation" => 8,
            "module defaults" => 9,
            "module texts" => 10,
            "module documentation" => 11,
            _ => 20,
        };

    private static string FormatWorkspaceKindTitle(string kind, bool isRu) =>
        (kind.ToLowerInvariant(), isRu) switch
        {
            ("module host", true) => "Конфигурация хоста (module host)",
            ("module catalog", true) => "Каталог отчётов (module catalog)",
            ("module dashboard", true) => "Дашборды (module dashboard)",
            ("module section", true) => "Секции и отчёты (module section)",
            ("module types", true) => "Типы строк данных (module types)",
            ("module flow", true) => "Потоки данных (module flow)",
            ("module diagram", true) => "Диаграммы (module diagram)",
            ("module layout", true) => "Сетки компоновки (module layout)",
            ("module presentation", true) => "Пресеты оформления (module presentation)",
            ("module defaults", true) => "Настройки по умолчанию (module defaults)",
            ("module texts", true) => "Локализация и тексты (module texts)",
            ("module documentation", true) => "Книги документации (module documentation)",
            _ => kind,
        };
}
