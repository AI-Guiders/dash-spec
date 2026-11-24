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
/// <c>///</c> doc comments and <c>module texts</c> localization tables across <c>stdlib</c>
/// and user <c>.ds</c> workspaces.
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
                    unitDocs.Add(doc);
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
                    typeDocs.Add(doc);
                }
                else if (resName.StartsWith("stdlib/rules/", StringComparison.OrdinalIgnoreCase))
                {
                    var enrichedChildren = doc.Children
                        .Select(c =>
                        {
                            var resolvedOpt = state.TryResolveRawText($"rule.{c.Id}");
                            if (string.IsNullOrWhiteSpace(c.Markdown) &&
                                FSharpOption<string>.get_IsSome(resolvedOpt))
                            {
                                return new DashSpecSymbolDoc(
                                    c.Kind,
                                    c.Id,
                                    c.Title,
                                    c.Namespace,
                                    c.Signature,
                                    resolvedOpt.Value,
                                    c.SourcePath,
                                    c.Line,
                                    c.ReferencedSymbols,
                                    c.Children);
                            }
                            return c;
                        })
                        .ToArray();
                    ruleDocs.Add(new DashSpecSymbolDoc(
                        doc.Kind,
                        doc.Id,
                        doc.Title,
                        doc.Namespace,
                        doc.Signature,
                        doc.Markdown,
                        doc.SourcePath,
                        doc.Line,
                        doc.ReferencedSymbols,
                        enrichedChildren));
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
        var compiledPages = parsed.Pages
            .Select(p => CompileBookPage(p, stdlib, workspace))
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
        DashSpecDocCatalog workspace)
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

        IReadOnlyList<DashSpecDocSection> symbolSections = catOpt switch
        {
            { IsWorkspace: true } => workspace.Sections,
            { IsAll: true } => stdlib.Sections.Concat(workspace.Sections).ToList(),
            not null => stdlib.Sections
                .Where(s => string.Equals(s.CategoryId, catId, StringComparison.OrdinalIgnoreCase))
                .ToList(),
            _ => [],
        };

        var subSections = page.Sections
            .Select(s => CompileBookPage(s, stdlib, workspace))
            .ToList();

        return new DashSpecCompiledBookPage(
            page.Id,
            page.Title,
            page.Markdown,
            catId,
            reportUnit,
            subSections,
            symbolSections);
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
            _ => kind,
        };
}
