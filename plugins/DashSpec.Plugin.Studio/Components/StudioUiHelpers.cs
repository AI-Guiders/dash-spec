#nullable enable

using DashSpec.CodeCenter;
using DashSpec.Execution.Compilation;
using DashSpec.Plugin.Studio.Services;
using Microsoft.FSharp.Core;

namespace DashSpec.Plugin.Studio.Components;

public sealed record StudioOutlineRow(
    string NodeKey,
    string Caption,
    string KindTag,
    string? SymbolId,
    int Depth,
    int Line,
    int Column,
    int EndLine)
{
    public IReadOnlyList<StudioOutlineRow> Children { get; init; } = [];
}

public sealed record StudioExplorerGroup(
    string Key,
    string Title,
    bool DefaultOpen,
    IReadOnlyList<DashSpecUnitDeclaration> Items);

public static class StudioUiHelpers
{
    public static (string Icon, string Color) GetUnitIconAndColor(string kind) =>
        kind.ToLowerInvariant() switch
        {
            "dashboard" => ("⊞", "#60a5fa"),
            "section" => ("▤", "#38bdf8"),
            "flow" => ("⇄", "#c084fc"),
            "types" => ("◈", "#f472b6"),
            "diagram" => ("◔", "#34d399"),
            "layout" => ("◫", "#fb923c"),
            "presentation" => ("✦", "#a78bfa"),
            "palette" => ("◑", "#facc15"),
            "defaults" => ("⚙", "#94a3b8"),
            "catalog" => ("❖", "#2dd4bf"),
            "host" => ("⌂", "#818cf8"),
            "runtime" => ("⚡", "#fbbf24"),
            "commands" => ("⌘", "#38bdf8"),
            "source" => ("⇄", "#10b981"),
            "transformer" => ("⇄", "#3b82f6"),
            "card" => ("⊞", "#60a5fa"),
            "filter" => ("◫", "#f59e0b"),
            "type" => ("◈", "#f472b6"),
            "field" => ("▪", "#94a3b8"),
            "port" => ("●", "#38bdf8"),
            "ports" => ("⚯", "#60a5fa"),
            "transform" => ("⚙", "#34d399"),
            "property" => ("▪", "#64748b"),
            "clause" => ("▫", "#94a3b8"),
            _ => ("◇", "#94a3b8"),
        };

    public static int GetKindSortOrder(string kind) =>
        kind.ToLowerInvariant() switch
        {
            "dashboard" => 0,
            "section" => 1,
            "flow" => 2,
            "types" => 3,
            "diagram" => 4,
            "layout" => 5,
            "presentation" => 6,
            "palette" => 7,
            "defaults" => 8,
            "catalog" => 9,
            "host" => 10,
            "runtime" => 11,
            _ => 20,
        };

    /// <summary>
    /// Projects the hierarchical <see cref="DashSpecProjectionHints.outlineTree"/> directly from the compiled <see cref="DashSpecConceptGraph"/>.
    /// </summary>
    public static IReadOnlyList<StudioOutlineRow> BuildOutlineTree(StudioDocumentInspection? inspection)
    {
        if (inspection is null)
        {
            return [];
        }

        StudioOutlineRow MapNode(DashSpecProjectionHints.OutlineTreeNode n)
        {
            var (line0, col0) = TextPositions.GetLineColumn(inspection.Text, n.Tier.Span.Start);
            var (endLine0, _) = TextPositions.GetLineColumn(inspection.Text, Math.Max(n.Tier.Span.Start, n.Tier.Span.End - 1));
            var symId = FSharpOption<string>.get_IsSome(n.Identifier) ? n.Identifier.Value : null;
            var key = $"{n.KindKeyword}:{n.Tier.Span.Start}";

            return new StudioOutlineRow(
                key,
                n.Caption,
                n.KindKeyword,
                symId,
                n.Depth,
                line0 + 1,
                col0 + 1,
                endLine0 + 1)
            {
                Children = n.Children.Select(MapNode).ToList(),
            };
        }

        var roots = DashSpecProjectionHints.outlineTree(inspection.ConceptGraph);
        // If there's a single module root node (`module flow ...`), unwrap its children so top-level blocks (`source`, `transformer`, `card`, `type`) sit at level 0
        if (roots.Length == 1 && roots[0].Children.Length > 0)
        {
            return roots[0].Children.Select(MapNode).ToList();
        }

        return roots.Select(MapNode).ToList();
    }

    public static IReadOnlyList<StudioOutlineRow> BuildSnapshotOutlineRows(StudioDocumentInspection? inspection)
    {
        var list = new List<StudioOutlineRow>();
        void Flatten(IEnumerable<StudioOutlineRow> nodes)
        {
            foreach (var n in nodes)
            {
                list.Add(n);
                Flatten(n.Children);
            }
        }

        Flatten(BuildOutlineTree(inspection));
        return list;
    }
}
