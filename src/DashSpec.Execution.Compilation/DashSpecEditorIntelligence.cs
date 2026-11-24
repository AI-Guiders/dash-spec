using DashSpec.Modeling.Core;
using DashSpec.Modeling.Parse;
using DashSpec.Modeling.Parse.Diagram;
using DashSpec.Modeling.Parse.Document;
using DashSpec.Modeling.Parse.Lexing;
using DashSpec.Modeling.Parse.Syntax;
using DashSpec.Modeling.Parse.Types;
using Microsoft.FSharp.Core;

namespace DashSpec.Execution.Compilation;

/// <summary>
/// Declarative DashSpec editor intelligence (DASHSPEC-ADR-0134):
/// delegates grammar transitions to <see cref="DashSpecGrammarGraph.expectedAt"/> and projects
/// <see cref="DashSpecGrammarGraph.GrammarExpectation"/> into <see cref="CompletionSuggestion"/>.
/// </summary>
public static class DashSpecEditorIntelligence
{
    public sealed record CompletionSuggestion(string Label, string InsertText, string? Kind = null, string? Detail = null);

    public sealed record DefinitionTarget(string FilePath, int Line, int Column);

    public sealed record WorkspaceUsage(string Path, int Line, int Column, int EndLine, int EndColumn, string Role = "reference");

    public static IReadOnlyList<CompletionSuggestion> GetCompletions(
        string filePath,
        string text,
        int line,
        int column,
        DashSpecWorkspaceIndex? workspaceIndex = null)
    {
        var offset = GetOffset(text, line, column);
        return GetCompletionsAtOffset(filePath, text, offset, workspaceIndex);
    }

    public static IReadOnlyList<CompletionSuggestion> GetCompletionsAtOffset(
        string filePath,
        string text,
        int caretOffset,
        DashSpecWorkspaceIndex? workspaceIndex = null)
    {
        UnitImport.ensureBuiltinUnitsRegistered();
        DashSpec.Modeling.Parse.DataFlow.TransformDeclarationParser.ensureStdlibRegistered();

        var tree = SyntaxTree.parse(text ?? string.Empty);
        var envOpt = UnitGrammar.tryUnwrapAny(tree.Root);
        var query = DashSpecGrammarGraph.expectedAt(tree, caretOffset);

        var items = new List<CompletionSuggestion>();
        foreach (var exp in query.Expectations)
        {
            ProjectExpectation(exp, query, envOpt, filePath, workspaceIndex, items);
        }

        return items
            .Where(c => string.IsNullOrEmpty(query.Prefix) ||
                        c.Label.StartsWith(query.Prefix, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(c => c.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void ProjectExpectation(
        DashSpecGrammarGraph.GrammarExpectation expectation,
        DashSpecGrammarGraph.GrammarQueryResult query,
        FSharpOption<UnitGrammar.UnitEnvelope> envOpt,
        string filePath,
        DashSpecWorkspaceIndex? workspaceIndex,
        List<CompletionSuggestion> results)
    {
        switch (expectation)
        {
            case DashSpecGrammarGraph.GrammarExpectation.Keyword kw:
                results.Add(Token(kw.word, "keyword", kw.detail));
                break;

            case DashSpecGrammarGraph.GrammarExpectation.PropertyKey prop:
                results.Add(Token(prop.spec.Name, "property", $"{prop.context} ({prop.spec.ValueType})"));
                break;

            case DashSpecGrammarGraph.GrammarExpectation.ClosingKeyword closing:
                results.Add(Token("end", "keyword", $"end {closing.word}"));
                results.Add(Token(closing.word, "keyword", $"Close '{closing.word}'"));
                break;

            case { IsUnitKindKeyword: true }:
            {
                var stdlib = DashSpecDocGenerator.BuildStdlibCatalog();
                foreach (var entry in ImportKindRegistry.unitManifest)
                {
                    var docText = stdlib.SymbolIndex.TryGetValue($"row.{entry.UnitKeyword}", out var rowDoc) &&
                                  !string.IsNullOrWhiteSpace(rowDoc.Markdown)
                        ? rowDoc.Markdown
                        : $"module {entry.UnitKeyword} <id>";
                    results.Add(Token(entry.UnitKeyword, "keyword", docText));
                }
                break;
            }

            case { IsImportKindKeyword: true }:
                foreach (var def in ImportKindRegistry.allDefinitions)
                {
                    results.Add(Token(def.Keyword, "keyword", $"import {def.Keyword} (module {def.ModuleKeyword})"));
                }
                break;

            case { IsTransformPluginId: true }:
            {
                var stdlib = DashSpecDocGenerator.BuildStdlibCatalog();
                foreach (var tr in TransformDeclarations.registered())
                {
                    var fallback = $"{tr.Locality} transform ({TransformDeclarations.TransformTypeEffectModule.toString(tr.TypeEffect)})";
                    var docText = stdlib.SymbolIndex.TryGetValue(tr.Id, out var trDoc) &&
                                  !string.IsNullOrWhiteSpace(trDoc.Markdown)
                        ? $"{fallback} — {trDoc.Markdown}"
                        : fallback;
                    results.Add(Token(tr.Id, "transform", docText));
                }
                break;
            }

            case { IsDiagramKindId: true }:
                foreach (var kindId in DiagramKindRegistry.knownKinds())
                {
                    results.Add(Token(kindId, "diagram", $"Diagram kind '{kindId}'"));
                }
                break;

            case DashSpecGrammarGraph.GrammarExpectation.ValueDomain dom:
                foreach (var val in dom.values)
                {
                    results.Add(Token(val, "value", dom.propertyKey));
                }
                break;

            case DashSpecGrammarGraph.GrammarExpectation.SymbolRef sym:
                ProjectSymbolRef(sym.kind, query, envOpt, filePath, workspaceIndex, results);
                break;
        }
    }

    private static void ProjectSymbolRef(
        DashSpecGrammarGraph.GrammarSymbolKind symbolKind,
        DashSpecGrammarGraph.GrammarQueryResult query,
        FSharpOption<UnitGrammar.UnitEnvelope> envOpt,
        string filePath,
        DashSpecWorkspaceIndex? workspaceIndex,
        List<CompletionSuggestion> results)
    {
        switch (symbolKind)
        {
            case DashSpecGrammarGraph.GrammarSymbolKind.UnitOfKind unitOfKind:
            {
                if (workspaceIndex is null)
                {
                    break;
                }

                var unitKind = unitOfKind.unitKeyword;
                var ids = unitKind.ToLowerInvariant() switch
                {
                    "diagram" => workspaceIndex.Diagrams.Keys,
                    "presentation" => workspaceIndex.Presentations.Keys,
                    _ => workspaceIndex.Units
                        .Where(u => u.Kind.Equals(unitKind, StringComparison.OrdinalIgnoreCase))
                        .Select(u => u.Id),
                };

                foreach (var id in ids.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    results.Add(Token(id, unitKind, $"module {unitKind}"));
                }
                break;
            }

            case { IsNamespace: true }:
            {
                var namespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (workspaceIndex is not null)
                {
                    foreach (var ns in workspaceIndex.UnitsByNamespace.Keys)
                    {
                        namespaces.Add(ns);
                    }
                }

                var specDir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(specDir) && Directory.Exists(specDir))
                {
                    foreach (var entry in UnitIndexModule.build(specDir).Entries)
                    {
                        if (!string.IsNullOrWhiteSpace(entry.Namespace))
                        {
                            namespaces.Add(entry.Namespace);
                        }
                    }
                }

                foreach (var ns in namespaces.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    results.Add(Token(ns, "namespace", "Declared namespace"));
                }
                break;
            }

            case { IsRowType: true }:
            {
                if (workspaceIndex is null)
                {
                    break;
                }

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var unit in workspaceIndex.Units.Where(u => u.Kind.Equals("types", StringComparison.OrdinalIgnoreCase)))
                {
                    var rawOpt = UnitSource.tryReadRaw(unit.Path);
                    if (!FSharpOption<string>.get_IsSome(rawOpt))
                    {
                        continue;
                    }

                    try
                    {
                        foreach (var def in TypeModuleParser.parseTypesModule(rawOpt.Value))
                        {
                            if (seen.Add(def.Name))
                            {
                                results.Add(Token(def.Name, "type", $"type {def.Name} ({def.Fields.Length} fields)"));
                            }
                        }
                    }
                    catch
                    {
                        // Ignore incomplete type file during edit
                    }
                }
                break;
            }

            case { IsDeclaredFilter: true }:
            {
                if (FSharpOption<UnitGrammar.UnitEnvelope>.get_IsSome(envOpt))
                {
                    foreach (var f in CollectDeclaredBlocks(envOpt.Value, static kw => kw.IsFilter))
                    {
                        results.Add(Token(f, "filter", "Declared filter"));
                    }
                }
                break;
            }

            case { IsDeclaredCard: true }:
            {
                if (FSharpOption<UnitGrammar.UnitEnvelope>.get_IsSome(envOpt))
                {
                    foreach (var c in CollectDeclaredBlocks(envOpt.Value, static kw => kw.IsCard))
                    {
                        results.Add(Token(c, "card", "Declared card"));
                    }
                }
                break;
            }

            case { IsDeclaredFlowNode: true }:
            {
                if (FSharpOption<UnitGrammar.UnitEnvelope>.get_IsSome(envOpt))
                {
                    foreach (var n in CollectDeclaredBlocks(envOpt.Value, static kw => kw.IsSource || kw.IsTransformer || kw.IsFlow))
                    {
                        results.Add(Token(n, "flow", "Declared flow node"));
                    }
                }
                break;
            }

            case DashSpecGrammarGraph.GrammarSymbolKind.DeclaredPort portRole:
            {
                if (FSharpOption<BlockDeclarationSyntax>.get_IsSome(query.EnclosingBlock))
                {
                    foreach (var p in query.EnclosingBlock.Value.Members.OfType<DashSpecAstNode.PortDeclaration>())
                    {
                        if (p.Item.Role.Equals(portRole.role))
                        {
                            results.Add(Token(p.Item.PortName, "port", $"{portRole.role} port"));
                        }
                    }
                }
                break;
            }
        }
    }

    public static DefinitionTarget? TryGetDefinition(
        string filePath,
        string text,
        int line,
        int column,
        DashSpecWorkspaceIndex? workspaceIndex = null)
    {
        if (string.IsNullOrEmpty(text) || workspaceIndex is null)
        {
            return null;
        }

        var tree = SyntaxTree.parse(text);
        var offset = GetOffset(text, line, column);
        var tokenOpt = FindIdentTokenAtOffset(tree, offset);
        if (tokenOpt is not { } token)
        {
            return null;
        }

        var symbol = token.Text;
        var ancestors = FindAncestorChainAtOffset(tree.Root, token.Span.Start);
        var leaf = ancestors.LastOrDefault();

        if (leaf is DashSpecAstNode.ImportDeclaration imp)
        {
            if (workspaceIndex.UnitsByNamespace.TryGetValue(imp.Item.Target, out var nsUnits) && nsUnits.Count > 0)
            {
                return ToTarget(nsUnits[0]);
            }
        }

        if (leaf is DashSpecAstNode.ClauseDeclaration clause)
        {
            if (clause.Item.Kind is ClauseKind.UsePresetClause usePreset &&
                workspaceIndex.UnitsById.TryGetValue(usePreset.target, out var presetUnit))
            {
                return ToTarget(presetUnit);
            }

            if (clause.Item.Kind is ClauseKind.UseClause useClause &&
                workspaceIndex.UnitsById.TryGetValue(useClause.target, out var usedUnit))
            {
                return ToTarget(usedUnit);
            }

            if (clause.Item.Kind is ClauseKind.DirectiveClause dirClause &&
                dirClause.args.Length > 0 &&
                workspaceIndex.UnitsById.TryGetValue(dirClause.args[^1], out var dirUnit))
            {
                return ToTarget(dirUnit);
            }
        }

        if (workspaceIndex.UnitsById.TryGetValue(symbol, out var byId))
        {
            return ToTarget(byId);
        }

        if (workspaceIndex.RowTypes.TryGetValue(symbol, out var rowTypeDef))
        {
            return new DefinitionTarget(rowTypeDef.Path, rowTypeDef.Line, rowTypeDef.Column);
        }

        if (workspaceIndex.UnitsByNamespace.TryGetValue(symbol, out var byNs) && byNs.Count > 0)
        {
            return ToTarget(byNs[0]);
        }

        return null;
    }

    public static IReadOnlyList<WorkspaceUsage> FindWorkspaceUsages(
        string symbolName,
        DashSpecWorkspaceIndex? workspaceIndex)
    {
        if (string.IsNullOrWhiteSpace(symbolName) || workspaceIndex is null)
        {
            return [];
        }

        var isKnownWorkspaceSymbol =
            workspaceIndex.UnitsById.ContainsKey(symbolName) ||
            workspaceIndex.RowTypes.ContainsKey(symbolName) ||
            workspaceIndex.UnitsByNamespace.ContainsKey(symbolName);
        if (!isKnownWorkspaceSymbol)
        {
            return [];
        }

        var usages = new List<WorkspaceUsage>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in workspaceIndex.Units.Select(u => u.Path)
                     .Concat(workspaceIndex.UnitsByNamespace.Values.SelectMany(v => v.Select(u => u.Path)))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string text;
            ParseTree? parsedTree = null;
            if (workspaceIndex.UnitSources.TryGetValue(path, out var cachedSource))
            {
                text = cachedSource.Text;
                parsedTree = cachedSource.Tree;
            }
            else
            {
                var rawOpt = UnitSource.tryReadRaw(path);
                if (!FSharpOption<string>.get_IsSome(rawOpt))
                {
                    continue;
                }

                text = rawOpt.Value;
            }

            if (!text.Contains(symbolName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var tok in DashSpecLexer.tokenize(text))
            {
                if ((tok.Kind == TokenKind.Ident || tok.Kind == TokenKind.Raw) &&
                    string.Equals(tok.Value, symbolName, StringComparison.OrdinalIgnoreCase))
                {
                    var (startLine, startCol) = TextPositions.GetLineColumn(text, tok.Start);
                    var (endLine, endCol) = TextPositions.GetLineColumn(text, tok.Start + tok.Length);
                    var key = $"{path}:{startLine + 1}:{startCol + 1}";
                    if (seen.Add(key))
                    {
                        parsedTree ??= SyntaxTree.parse(text);
                        var role = DashSpecAst.outlineLabelAt(tok.Start, parsedTree.Root);
                        usages.Add(new WorkspaceUsage(path, startLine + 1, startCol + 1, endLine + 1, endCol + 1, role));
                    }
                }
            }
        }

        return usages;
    }

    public static DashSpecWorkspaceIndex ResolveWorkspaceIndex(string? projectRoot, string filePath)
    {
        if (!string.IsNullOrWhiteSpace(projectRoot) && Directory.Exists(projectRoot))
        {
            return DashSpecCompilerServicesHost.GetCorpus(projectRoot).GetWorkspaceIndex();
        }

        if (!string.IsNullOrWhiteSpace(filePath))
        {
            var resolvedProject = DashSpec.Modeling.Parse.Project.DashSpecProjectLocator.resolveProjectRoot(filePath);
            if (Directory.Exists(resolvedProject))
            {
                return DashSpecCompilerServicesHost.GetCorpus(resolvedProject).GetWorkspaceIndex();
            }
        }

        return new DashSpecWorkspaceIndex();
    }

    private static CompletionSuggestion Token(string token, string kind, string detail) =>
        new(token, token, kind, detail);

    private static IEnumerable<string> CollectDeclaredBlocks(
        UnitGrammar.UnitEnvelope env,
        Func<DashSpecBlockKeyword, bool> predicate)
    {
        foreach (var node in DashSpecAst.descendants(DashSpecAstNode.NewModuleDeclaration(env.ModuleNode)))
        {
            if (node is DashSpecAstNode.BlockDeclaration b &&
                b.Item.Opener is DashSpecBlockOpener.Named named &&
                predicate(named.keyword))
            {
                yield return named.identifier;
            }
        }
    }

    private static IReadOnlyList<DashSpecAstNode> FindAncestorChainAtOffset(DashSpecAstNode root, int offset)
    {
        var chain = new List<DashSpecAstNode>();
        var probe = Math.Max(0, offset - 1);

        void Walk(DashSpecAstNode node)
        {
            var span = DashSpecAst.span(node);
            if ((span.Start <= offset && offset <= span.End) ||
                (span.Start <= probe && probe <= span.End))
            {
                chain.Add(node);
                foreach (var child in DashSpecAst.members(node))
                {
                    Walk(child);
                }
            }
        }

        Walk(root);
        return chain;
    }

    private static SyntaxToken? FindIdentTokenAtOffset(ParseTree tree, int offset)
    {
        foreach (var tok in tree.Tokens)
        {
            if ((tok.LexKind == TokenKind.Ident || tok.LexKind == TokenKind.Raw) &&
                tok.Span.Start <= offset &&
                offset <= tok.Span.End)
            {
                return tok;
            }
        }

        return null;
    }

    private static int GetOffset(string text, int line, int column)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var currentLine = 1;
        var lineStart = 0;
        for (var i = 0; i < text.Length && currentLine < line; i++)
        {
            if (text[i] == '\n')
            {
                currentLine++;
                lineStart = i + 1;
            }
        }

        var lineEnd = lineStart;
        while (lineEnd < text.Length && text[lineEnd] != '\n' && text[lineEnd] != '\r')
        {
            lineEnd++;
        }

        return Math.Clamp(lineStart + Math.Max(0, column - 1), lineStart, lineEnd);
    }

    private static DefinitionTarget? ToTarget(DashSpecUnitDeclaration unit) =>
        string.IsNullOrWhiteSpace(unit.Path) || !File.Exists(unit.Path)
            ? null
            : new DefinitionTarget(unit.Path, unit.Line, 1);
}
