using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

namespace DashSpec.Core.Layout;

public static class PlacementDefaults
{
    public static PlacementDefinition ForFamily(DiagramDataFamily family, int columns) =>
        family is DiagramDataFamily.Table or DiagramDataFamily.Matrix
            ? new PlacementDefinition(Row: 1, Col: 1, Span: columns)
            : new PlacementDefinition(Span: columns / 2);
}
