using System.Reflection;
using System.Text;
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
        UnitImport.ensureBuiltinUnitsRegistered();
        var lang = NormalizeLanguage(language);
        var textLookup = LoadStdlibTexts(lang);
        FSharpFunc<string, FSharpOption<string>> resolver = FuncConvert.FromFunc<string, FSharpOption<string>>(
            key => textLookup.TryGetValue(key, out var val) ? FSharpOption<string>.Some(val) : FSharpOption<string>.None);

        var unitDocs = new List<DashSpecSymbolDoc>();
        var transformDocs = new List<DashSpecSymbolDoc>();
        var presentationDocs = new List<DashSpecSymbolDoc>();
        var typeDocs = new List<DashSpecSymbolDoc>();
        var ruleDocs = new List<DashSpecSymbolDoc>();
        var monitoringDocs = new List<DashSpecSymbolDoc>();

        var assemblies = new[]
        {
            typeof(ImportKindRegistry).Assembly,
            typeof(DashSpecDocumentationExtractor).Assembly,
            typeof(DashSpec.Execution.Parsing.DashSpecParser).Assembly,
        };

        foreach (var asm in assemblies.Distinct())
        {
            foreach (var resName in asm.GetManifestResourceNames().OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                if (!resName.StartsWith("stdlib/", StringComparison.OrdinalIgnoreCase) ||
                    !resName.EndsWith(".ds", StringComparison.OrdinalIgnoreCase) ||
                    resName.StartsWith("stdlib/texts/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                using var stream = asm.GetManifestResourceStream(resName);
                if (stream is null)
                {
                    continue;
                }

                using var reader = new StreamReader(stream);
                var text = reader.ReadToEnd();
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
                                if (string.IsNullOrWhiteSpace(c.Markdown) &&
                                    textLookup.TryGetValue($"rule.{c.Id}", out var ruleText))
                                {
                                    return new DashSpecSymbolDoc(
                                        c.Kind,
                                        c.Id,
                                        c.Title,
                                        c.Namespace,
                                        c.Signature,
                                        ruleText,
                                        c.SourcePath,
                                        c.Line,
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
                            enrichedChildren));
                    }
                    else if (resName.StartsWith("stdlib/monitoring/", StringComparison.OrdinalIgnoreCase))
                    {
                        monitoringDocs.Add(doc);
                    }
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
        var files = Directory
            .EnumerateFiles(fullRoot, "*.ds", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var textLookup = LoadStdlibTexts(lang);
        foreach (var file in files)
        {
            try
            {
                var raw = File.ReadAllText(file);
                var tree = SyntaxTree.parse(raw);
                var envOpt = DashSpec.Modeling.Parse.UnitGrammar.tryUnwrapAny(tree.Root);
                if (FSharpOption<DashSpec.Modeling.Parse.UnitGrammar.UnitEnvelope>.get_IsSome(envOpt) &&
                    envOpt.Value.Kind.IsTexts)
                {
                    var doc = TextsModuleParser.parseEnvelope(envOpt.Value);
                    var docLang = FSharpOption<string>.get_IsSome(doc.Language) ? doc.Language.Value : "";
                    if (string.IsNullOrWhiteSpace(docLang) ||
                        docLang.StartsWith(lang[..2], StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var b in doc.Bindings)
                        {
                            var k = FSharpOption<LanguageObjectKind>.get_IsSome(b.Target.Kind)
                                ? $"{b.Target.Kind.Value.ToString().ToLowerInvariant()}.{b.Target.Path.Text}"
                                : b.Target.Path.Text;
                            textLookup[k] = b.Label;
                        }
                    }
                }
            }
            catch
            {
                // Ignore incomplete draft files during doc scan
            }
        }

        FSharpFunc<string, FSharpOption<string>> resolver = FuncConvert.FromFunc<string, FSharpOption<string>>(
            key => textLookup.TryGetValue(key, out var val) ? FSharpOption<string>.Some(val) : FSharpOption<string>.None);

        var byKind = new Dictionary<string, List<DashSpecSymbolDoc>>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            try
            {
                var raw = File.ReadAllText(file);
                var relPath = Path.GetRelativePath(fullRoot, file).Replace('\\', '/');
                var extracted = DashSpecDocumentationExtractor.extractFromText(
                    FSharpOption<string>.Some(relPath),
                    resolver,
                    raw);
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

    private static Dictionary<string, string> LoadStdlibTexts(string language)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var asm = typeof(ImportKindRegistry).Assembly;
        var isRu = language.StartsWith("ru", StringComparison.OrdinalIgnoreCase);
        var preferredRes = isRu ? "stdlib/texts/rules.ru.ds" : "stdlib/texts/rules.en.ds";

        foreach (var resName in new[] { "stdlib/texts/rules.en.ds", preferredRes }.Distinct())
        {
            using var stream = asm.GetManifestResourceStream(resName);
            if (stream is null)
            {
                continue;
            }

            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd();
            try
            {
                var doc = TextsModuleParser.parse(text);
                foreach (var b in doc.Bindings)
                {
                    var key = FSharpOption<LanguageObjectKind>.get_IsSome(b.Target.Kind)
                        ? $"{b.Target.Kind.Value.ToString().ToLowerInvariant()}.{b.Target.Path.Text}"
                        : b.Target.Path.Text;
                    map[key] = b.Label;
                }
            }
            catch
            {
                // Ignore parse issues
            }
        }

        return map;
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
