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
        foreach (var slot in slots)
        {
            var placement = slot.Placement;
            builder.AppendLine(
                $"    {slot.Node.Kind} '{slot.Node.Id}' @ (row {placement.Row}, col {placement.Col}, span {placement.Span}) " +
                $"scope {slot.Scope} {ProvenanceLabel(slot.Provenance)}");
        }
    }

    static string ProvenanceLabel(PlacementProvenance provenance) =>
        provenance == PlacementProvenance.Declared ? "[declared]" : "[recorded default]";

    static string RouteLabel(BoardRoute route) =>
        $"tab '{OptionId(route.TabId)}' page '{OptionId(route.PageId)}'";

    static string OptionId(Microsoft.FSharp.Core.FSharpOption<string> option) =>
        option is null ? "" : option.Value ?? "";
}
