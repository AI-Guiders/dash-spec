#nullable enable

using System.Text.Json;
using System.Text.Json.Serialization;
using AIGuiders.Platform.Modeling.Language;
using DashSpec.Execution.Compilation;
using DashSpec.Modeling.Core;
using DashSpec.Modeling.Language.Adapters.DashSpec;
using DashSpec.Modeling.Parse.Syntax;

namespace DashSpec.Plugin.Studio.Services;

public sealed record StudioLspDiagnosticDto(
    int StartLineNumber,
    int StartColumn,
    int EndLineNumber,
    int EndColumn,
    string Message,
    string Severity,
    string Code);

public sealed record StudioLspCompletionDto(
    string Label,
    string InsertText,
    string Kind,
    string? Detail);

public sealed record StudioLspDefinitionDto(
    string RelativePath,
    int LineNumber,
    int Column);

public sealed record StudioLspSymbolDto(
    string Name,
    string Kind,
    int StartLineNumber,
    int StartColumn,
    int EndLineNumber,
    int EndColumn);

public sealed record StudioReferenceItemDto(
    string SymbolName,
    string RelativePath,
    int LineNumber,
    int Column,
    int EndColumn,
    string LinePreview);

public sealed record StudioHoverDto(
    string Title,
    string Detail,
    string? DefinedInPath,
    int? DefinedAtLine);

/// <summary>
/// Builds Monaco LSP payloads (diagnostics markers, completions, definitions, references, symbols)
/// and Apache ECharts <c>type: "graph"</c> options directly from typed <see cref="WiringGraph"/> and <see cref="FlowGraph"/>.
/// </summary>
public static class StudioGraphAndLspBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static IReadOnlyList<StudioLspDiagnosticDto> BuildMonacoMarkers(StudioDocumentInspection inspection)
    {
        return inspection.FileDiagnostics
            .Select(d => new StudioLspDiagnosticDto(
                Math.Max(1, d.Span.Line),
                Math.Max(1, d.Span.Column),
                Math.Max(1, d.Span.EndLine),
                Math.Max(2, d.Span.EndColumn),
                d.Message,
                d.Severity == Severity.Error ? "error" : d.Severity == Severity.Warning ? "warning" : "info",
                d.Id))
            .ToList();
    }

    public static IReadOnlyList<StudioLspSymbolDto> BuildMonacoSymbols(string relativePath, string text)
    {
        try
        {
            return DashSpecSymbolIndex.collectDefinitions(relativePath, text)
                .Select(s => new StudioLspSymbolDto(
                    s.Name,
                    s.Kind,
                    Math.Max(1, s.Span.Line),
                    Math.Max(1, s.Span.Column),
                    Math.Max(1, s.Span.EndLine),
                    Math.Max(2, s.Span.EndColumn)))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public static async Task<IReadOnlyList<StudioLspCompletionDto>> GetCompletionsAsync(
        StudioWorkspaceService workspace,
        string relativePath,
        string text,
        int lineNumber,
        int column,
        CancellationToken ct = default)
    {
        var root = workspace.ResolveWorkspaceRoot();
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        var req = new LanguageRequest(fullPath, lineNumber, column, text, root, LanguageIds.Dashspec);
        var result = await workspace.LanguageBackend.GetCompletionsAsync(req, ct);
        return result.Items
            .Select(s => new StudioLspCompletionDto(s.Label, s.InsertText, s.Kind ?? "keyword", s.Detail))
            .ToList();
    }

    public static async Task<StudioLspDefinitionDto?> ResolveDefinitionAsync(
        StudioWorkspaceService workspace,
        string relativePath,
        string text,
        int lineNumber,
        int column,
        CancellationToken ct = default)
    {
        var root = workspace.ResolveWorkspaceRoot();
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        var req = new LanguageRequest(fullPath, lineNumber, column, text, root, LanguageIds.Dashspec);
        var nav = await workspace.LanguageBackend.GoToDefinitionAsync(req, ct);
        if (nav is null || nav.Declarations is null || nav.Declarations.Length == 0 || string.IsNullOrWhiteSpace(nav.Definition.Path))
        {
            return null;
        }

        return new StudioLspDefinitionDto(
            workspace.ToRelativePath(nav.Definition.Path),
            Math.Max(1, nav.Definition.Line),
            Math.Max(1, nav.Definition.Column));
    }

    public static async Task<IReadOnlyList<StudioLspDefinitionDto>> FindReferencesAsync(
        StudioWorkspaceService workspace,
        string relativePath,
        string text,
        int lineNumber,
        int column,
        CancellationToken ct = default)
    {
        var root = workspace.ResolveWorkspaceRoot();
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        var req = new LanguageRequest(fullPath, lineNumber, column, text, root, LanguageIds.Dashspec);
        var res = await workspace.LanguageBackend.FindUsagesAsync(req, ct);
        return res.References
            .Select(r => new StudioLspDefinitionDto(
                workspace.ToRelativePath(r.Span.Path),
                Math.Max(1, r.Span.Line),
                Math.Max(1, r.Span.Column)))
            .ToList();
    }

    public static async Task<IReadOnlyList<StudioReferenceItemDto>> FindRichReferencesAsync(
        StudioWorkspaceService workspace,
        string relativePath,
        string text,
        int lineNumber,
        int column,
        CancellationToken ct = default)
    {
        var root = workspace.ResolveWorkspaceRoot();
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        var req = new LanguageRequest(fullPath, lineNumber, column, text, root, LanguageIds.Dashspec);
        var sym = await workspace.LanguageBackend.GetSymbolAtPositionAsync(req, ct);
        var symbolName = sym is not null && !string.IsNullOrWhiteSpace(sym.Name) ? sym.Name : "symbol";

        var res = await workspace.LanguageBackend.FindUsagesAsync(req, ct);
        if (res?.References is null)
        {
            return [];
        }
        return res.References
            .Select(r => new StudioReferenceItemDto(
                symbolName,
                workspace.ToRelativePath(r.Span.Path),
                Math.Max(1, r.Span.Line),
                Math.Max(1, r.Span.Column),
                Math.Max(2, r.Span.EndColumn),
                r.Kind))
            .ToList();
    }

    public static async Task<StudioHoverDto?> BuildHoverInfoAsync(
        StudioWorkspaceService workspace,
        string relativePath,
        string text,
        int lineNumber,
        int column,
        CancellationToken ct = default)
    {
        var root = workspace.ResolveWorkspaceRoot();
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        var req = new LanguageRequest(fullPath, lineNumber, column, text, root, LanguageIds.Dashspec);
        var sym = await workspace.LanguageBackend.GetSymbolAtPositionAsync(req, ct);
        if (sym is null || string.IsNullOrWhiteSpace(sym.Name))
        {
            return null;
        }

        var nav = await workspace.LanguageBackend.GoToDefinitionAsync(req, ct);
        var defRel = nav is not null && nav.Declarations is { Length: > 0 } && !string.IsNullOrWhiteSpace(nav.Definition.Path)
            ? workspace.ToRelativePath(nav.Definition.Path)
            : relativePath;
        var defLine = nav is not null && nav.Declarations is { Length: > 0 } ? nav.Definition.Line : sym.Span.Line;

        return new StudioHoverDto(
            $"{sym.Kind} {sym.Name}",
            $"Определён в `{defRel}:{defLine}`",
            defRel,
            defLine);
    }

    /// <summary>
    /// Encodes <see cref="SyntaxTree.classifiedSpans"/> (<see cref="DashSpecSyntaxKind"/>) into Monaco's
    /// 5-integer relative SemanticTokens <c>data</c> array: <c>[deltaLine, deltaStartChar, length, tokenType, tokenModifiers]</c>.
    /// </summary>
    public static int[] BuildMonacoSemanticTokensData(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var tree = SyntaxTree.parse(text);
        var spans = SyntaxTree.classifiedSpans(tree);
        if (spans.Count == 0)
        {
            return [];
        }

        var lineStarts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                lineStarts.Add(i + 1);
            }
        }

        var startsArr = lineStarts.ToArray();
        var data = new List<int>(spans.Count * 5);
        var prevLine = 0;
        var prevChar = 0;

        foreach (var span in spans)
        {
            if (span.Start < 0 || span.Start >= text.Length || span.Length <= 0)
            {
                continue;
            }

            // Do not emit multi-line tokens as a single span in Monaco semantic tokens
            var endOffset = Math.Min(text.Length, span.Start + span.Length);
            var lineIdx = Array.BinarySearch(startsArr, span.Start);
            if (lineIdx < 0)
            {
                lineIdx = Math.Max(0, ~lineIdx - 1);
            }

            var lineStart = startsArr[lineIdx];
            var nextLineStart = lineIdx + 1 < startsArr.Length ? startsArr[lineIdx + 1] : text.Length;
            var singleLineLength = Math.Min(span.Length, Math.Max(1, nextLineStart - span.Start));
            var startChar = span.Start - lineStart;

            var deltaLine = lineIdx - prevLine;
            var deltaStart = deltaLine == 0 ? startChar - prevChar : startChar;
            if (deltaLine < 0 || deltaStart < 0)
            {
                continue;
            }

            var tokenTypeIndex = (int)span.Kind;
            data.Add(deltaLine);
            data.Add(deltaStart);
            data.Add(singleLineLength);
            data.Add(tokenTypeIndex);
            data.Add(0);

            prevLine = lineIdx;
            prevChar = startChar;
        }

        return data.ToArray();
    }

    /// <summary>
    /// Returns the list of graph node IDs available for symbol neighborhood filtering in the current document.
    /// </summary>
    public static IReadOnlyList<string> GetGraphNodeIds(StudioDocumentInspection? inspection)
    {
        if (inspection is null)
        {
            return [];
        }

        if (inspection.FlowGraph is { } flowGraph)
        {
            return flowGraph.Nodes.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
        }

        if (inspection.WiringGraph is { } wiringGraph)
        {
            return wiringGraph.Nodes.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
        }

        return [];
    }

    /// <summary>
    /// Converts a typed <see cref="WiringGraph"/> or <see cref="FlowGraph"/> into an Apache ECharts <c>type: "graph"</c> JSON option.
    /// When <paramref name="focusSymbol"/> is provided, builds the ego-network (1..2 hop neighborhood) around that symbol.
    /// </summary>
    public static string BuildEChartsGraphOptionJson(StudioDocumentInspection inspection, string? focusSymbol = null)
    {
        if (inspection.FlowGraph is { } flowGraph)
        {
            var sliced = string.IsNullOrWhiteSpace(focusSymbol)
                ? flowGraph
                : FlowGraphModule.sliceNeighborhood(focusSymbol, 2, flowGraph);
            return BuildFlowGraphOption(sliced, focusSymbol);
        }

        if (inspection.WiringGraph is { } wiringGraph)
        {
            var sliced = string.IsNullOrWhiteSpace(focusSymbol)
                ? wiringGraph
                : WiringGraphModule.sliceNeighborhood(focusSymbol, 1, wiringGraph);
            return BuildWiringGraphOption(sliced, focusSymbol);
        }

        return BuildConceptAstGraphOption(inspection);
    }

    private static string BuildFlowGraphOption(FlowGraph graph, string? focusSymbol = null)
    {
        var categories = new[]
        {
            new { name = "Source", itemStyle = new { color = "#10b981" } },
            new { name = "Transformer", itemStyle = new { color = "#3b82f6" } },
            new { name = "Composite", itemStyle = new { color = "#a855f7" } },
        };

        var nodes = new List<object>();
        var sources = graph.Nodes.Values.Where(n => n.Kind.IsSource).OrderBy(n => n.Id).ToList();
        var transformers = graph.Nodes.Values.Where(n => !n.Kind.IsSource).OrderBy(n => n.Id).ToList();

        for (var i = 0; i < sources.Count; i++)
        {
            var n = sources[i];
            var isFocused = !string.IsNullOrWhiteSpace(focusSymbol) && n.Id.Equals(focusSymbol, StringComparison.OrdinalIgnoreCase);
            var outPorts = string.Join(", ", n.Outputs.Select(p => $"{p.Name}: {FormatPortType(p.Type)}"));
            nodes.Add(new
            {
                id = n.Id,
                name = n.Id,
                category = 0,
                x = 120,
                y = 90 + i * 110,
                symbol = "roundRect",
                symbolSize = isFocused ? new[] { 180, 48 } : new[] { 155, 42 },
                itemStyle = isFocused ? new { borderColor = "#fbbf24", borderWidth = 3 } : null,
                value = $"source {n.Id} → [{outPorts}]",
            });
        }

        for (var i = 0; i < transformers.Count; i++)
        {
            var n = transformers[i];
            var isFocused = !string.IsNullOrWhiteSpace(focusSymbol) && n.Id.Equals(focusSymbol, StringComparison.OrdinalIgnoreCase);
            var outPorts = string.Join(", ", n.Outputs.Select(p => $"{p.Name}: {FormatPortType(p.Type)}"));
            nodes.Add(new
            {
                id = n.Id,
                name = n.Id,
                category = n.Kind.IsComposite ? 2 : 1,
                x = 460 + (i % 2) * 230,
                y = 90 + (i / 2) * 110,
                symbol = "roundRect",
                symbolSize = isFocused ? new[] { 185, 48 } : new[] { 165, 42 },
                itemStyle = isFocused ? new { borderColor = "#fbbf24", borderWidth = 3 } : null,
                value = $"transformer {n.Id} → [{outPorts}]",
            });
        }

        var links = graph.Edges
            .Select(e => new
            {
                source = e.From.NodeId,
                target = e.To.NodeId,
                value = $"{e.From.PortName} → {e.To.PortName}",
                label = new
                {
                    show = true,
                    formatter = $"{e.From.PortName} → {e.To.PortName}",
                    fontSize = 10,
                    color = "#94a3b8",
                },
                lineStyle = new
                {
                    color = "#38bdf8",
                    width = 2,
                    curveness = 0.14,
                },
            }).Cast<object>().ToList();

        return SerializeGraphOption("FlowGraph (Data Plane)", categories, nodes, links);
    }

    private static string BuildWiringGraphOption(WiringGraph graph, string? focusSymbol = null)
    {
        var categories = new[]
        {
            new { name = "Filter", itemStyle = new { color = "#f59e0b" } },
            new { name = "Flow / Source", itemStyle = new { color = "#10b981" } },
            new { name = "Card", itemStyle = new { color = "#3b82f6" } },
            new { name = "Slot", itemStyle = new { color = "#6366f1" } },
            new { name = "Chrome / Page", itemStyle = new { color = "#ec4899" } },
        };

        int CategoryOf(WiringNodeKind kind)
        {
            if (kind.IsFilter) return 0;
            if (kind.IsModuleNode) return 1;
            if (kind.IsCard || kind.IsGroup || kind.IsNest) return 2;
            if (kind.IsSlot) return 3;
            return 4;
        }

        var grouped = graph.Nodes.Values
            .GroupBy(n => CategoryOf(n.Kind))
            .ToDictionary(g => g.Key, g => g.OrderBy(n => n.Id).ToList());

        var columnX = new Dictionary<int, int>
        {
            [0] = 90,
            [1] = 290,
            [2] = 520,
            [3] = 750,
            [4] = 970,
        };

        var nodes = new List<object>();
        foreach (var (cat, list) in grouped)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var n = list[i];
                var isFocused = !string.IsNullOrWhiteSpace(focusSymbol) && n.Id.Equals(focusSymbol, StringComparison.OrdinalIgnoreCase);
                var scope = Microsoft.FSharp.Core.FSharpOption<string>.get_IsSome(n.Scope) ? n.Scope.Value : "";
                nodes.Add(new
                {
                    id = n.Id,
                    name = n.Id,
                    category = cat,
                    x = columnX.GetValueOrDefault(cat, 500),
                    y = 65 + i * 62,
                    symbol = "roundRect",
                    symbolSize = isFocused ? new[] { 172, 38 } : new[] { 150, 32 },
                    itemStyle = isFocused ? new { borderColor = "#fbbf24", borderWidth = 3 } : null,
                    value = $"{n.Kind} ({scope})",
                });
            }
        }

        var links = graph.Edges
            .Select(e =>
        {
            var fromPort = Microsoft.FSharp.Core.FSharpOption<string>.get_IsSome(e.FromPort) ? e.FromPort.Value : "";
            var toPort = Microsoft.FSharp.Core.FSharpOption<string>.get_IsSome(e.ToPort) ? e.ToPort.Value : "";
            var labelText = string.IsNullOrEmpty(fromPort) && string.IsNullOrEmpty(toPort)
                ? e.Kind.ToString()
                : $"{fromPort} → {toPort}".Trim(' ', '→');
            var edgeColor = e.Kind.IsFlow ? "#10b981" : e.Kind.IsEvent ? "#f97316" : "#60a5fa";

            return new
            {
                source = e.From,
                target = e.To,
                value = labelText,
                label = new
                {
                    show = !string.IsNullOrEmpty(fromPort) || e.Kind.IsEvent,
                    formatter = labelText,
                    fontSize = 9,
                    color = "#94a3b8",
                },
                lineStyle = new
                {
                    color = edgeColor,
                    width = e.Kind.IsFlow ? 2.2 : 1.5,
                    type = e.Kind.IsEvent ? "dashed" : "solid",
                    curveness = 0.12,
                },
            };
        }).Cast<object>().ToList();

        return SerializeGraphOption("WiringGraph (Document IR)", categories, nodes, links);
    }

    private static string BuildConceptAstGraphOption(StudioDocumentInspection inspection)
    {
        var categories = new[]
        {
            new { name = "Module / Outline", itemStyle = new { color = "#3b82f6" } },
            new { name = "Diagram", itemStyle = new { color = "#10b981" } },
            new { name = "FormField", itemStyle = new { color = "#f59e0b" } },
        };

        var snapNodes = inspection.Snapshot.Nodes.Values
            .OrderBy(n => n.Start)
            .Take(60)
            .ToList();

        var nodes = new List<object>();
        var links = new List<object>();

        for (var i = 0; i < snapNodes.Count; i++)
        {
            var n = snapNodes[i];
            var idStr = n.Start.ToString();
            var hasParent = Microsoft.FSharp.Core.FSharpOption<
                AIGuiders.Platform.Modeling.Core.Identity.Identity<
                    AIGuiders.Platform.Modeling.Core.Identity.SyntaxNode,
                    AIGuiders.Platform.Modeling.Core.Identity.NumericId>>.get_IsSome(n.Parent);

            nodes.Add(new
            {
                id = idStr,
                name = $"{n.Name} @{n.Start}",
                category = hasParent ? 2 : 0,
                x = (hasParent ? 360 : 120) + (i % 3) * 170,
                y = 65 + (i / 3) * 58,
                symbol = "roundRect",
                symbolSize = new[] { 155, 30 },
                value = $"[{n.Start}..{n.End}]",
            });

            if (hasParent &&
                n.Parent is not null &&
                Microsoft.FSharp.Collections.MapModule.TryFind(n.Parent.Value, inspection.Snapshot.Nodes) is { } parentOpt)
            {
                links.Add(new
                {
                    source = parentOpt.Value.Start.ToString(),
                    target = idStr,
                    lineStyle = new { color = "#64748b", width = 1.4, curveness = 0.1 },
                });
            }
        }

        return SerializeGraphOption("CodeCenter ConceptGraph", categories, nodes, links);
    }

    private static string SerializeGraphOption(
        string title,
        object categories,
        List<object> nodes,
        List<object> links)
    {
        var option = new
        {
            backgroundColor = "transparent",
            tooltip = new { trigger = "item" },
            legend = new[]
            {
                new
                {
                    top = 6,
                    left = 10,
                    textStyle = new { color = "#cbd5e1", fontSize = 11 },
                },
            },
            series = new[]
            {
                new
                {
                    name = title,
                    type = "graph",
                    layout = "none",
                    roam = true,
                    draggable = true,
                    focusNodeAdjacency = true,
                    edgeSymbol = new[] { "none", "arrow" },
                    edgeSymbolSize = new[] { 4, 8 },
                    categories,
                    data = nodes,
                    links,
                    label = new
                    {
                        show = true,
                        position = "inside",
                        color = "#f8fafc",
                        fontSize = 11,
                        overflow = "truncate",
                        width = 138,
                    },
                    emphasis = new
                    {
                        focus = "adjacency",
                        lineStyle = new { width = 3.5 },
                    },
                },
            },
        };

        return JsonSerializer.Serialize(option, JsonOptions);
    }

    private static string FormatPortType(DashPortType portType) =>
        portType.ToString();
}
