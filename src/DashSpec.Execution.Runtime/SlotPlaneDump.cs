using System.Text;
using DashSpec.Modeling.Core;
using DashSpec.Modeling.Parse.Layout;

namespace DashSpec.Execution.Runtime;

/// <summary>
/// Human-readable dump of an assembled <see cref="SlotPlane"/> ([ADR-0111] P0):
/// for the eyes and the conformance fixtures — собрал, посмотрел композицию целиком.
/// </summary>
public static class SlotPlaneDump
{
    public static string Dump(SlotPlane plane)
    {
        ArgumentNullException.ThrowIfNull(plane);

        var builder = new StringBuilder();
        builder.AppendLine($"slot plane: initial entry '{plane.InitialRoute.EntryId}' tab '{plane.InitialRoute.TabId}' page '{plane.InitialRoute.PageId}'");

        builder.AppendLine("boards:");
        foreach (var (route, slots) in plane.Boards.OrderBy(kv => RouteLabel(kv.Key), StringComparer.Ordinal))
        {
            DumpSlice(builder, $"  [{RouteLabel(route)}]", slots);
        }

        builder.AppendLine("toolbars:");
        foreach (var (route, slots) in plane.Toolbars.OrderBy(kv => RouteLabel(kv.Key), StringComparer.Ordinal))
        {
            DumpSlice(builder, $"  [{RouteLabel(route)}]", slots);
        }

        builder.AppendLine("interiors:");
        foreach (var (cardId, slots) in plane.CardInteriors.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            DumpSlice(builder, $"  card '{cardId}'", slots);
        }

        builder.AppendLine("chrome:");
        foreach (var (cardId, slots) in plane.CardFilterChrome.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            DumpSlice(builder, $"  card '{cardId}'", slots, hideEmpty: false);
        }

        return builder.ToString();
    }

    static void DumpSlice(StringBuilder builder, string label, IReadOnlyList<Slot> slots, bool hideEmpty = true)
    {
        if (hideEmpty && slots.Count == 0)
        {
            return;
        }

        builder.AppendLine(label);
        DumpSlots(builder, slots, indent: 4);
    }

    static void DumpSlots(StringBuilder builder, IReadOnlyList<Slot> slots, int indent)
    {
        var pad = new string(' ', indent);
        foreach (var slot in slots)
        {
            var variants = slot.Placement;
            var innerTag = slot.Inner?.Value is { } inner ? $" -> [{inner.Rows}x{inner.Columns}]({inner.Slots.Count})" : "";
            builder.AppendLine(
                $"{pad}{slot.Node.Kind} '{slot.Node.Id}' @ (row {variants.Wide.Row}, col {variants.Wide.Col}, span {variants.Wide.Span}" +
                $" | mid span {variants.Mid.Span} | narrow span {variants.Narrow.Span}) " +
                $"scope {slot.Scope} {ProvenanceLabel(slot.Provenance)}{innerTag}");
            if (slot.Inner?.Value is { } childContainer && childContainer.Slots.Count > 0)
            {
                DumpSlots(builder, childContainer.Slots, indent + 2);
            }
        }
    }

    static string ProvenanceLabel(PlacementProvenance provenance) =>
        provenance == PlacementProvenance.Declared ? "[declared]" : "[recorded default]";

    static string RouteLabel(BoardRoute route) =>
        $"tab '{OptionId(route.TabId)}' page '{OptionId(route.PageId)}'";

    static string OptionId(Microsoft.FSharp.Core.FSharpOption<string> option) =>
        option is null ? "" : option.Value ?? "";
}
