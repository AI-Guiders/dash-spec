using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

namespace DashSpec.Core.Runtime;

public sealed record MatrixPresentation(
    int HeightPx = 320,
    string? XLabel = null,
    string? YLabel = null,
    string? ValueLabel = null,
    string? TooltipLabel = null,
    TooltipFormat TooltipFormat = TooltipFormat.Inline,
    string TooltipSplit = ", ",
    string XFormat = "date.short",
    string YFormat = "user.short",
    string ColorScale = "heat",
    LegendDefinition? Legend = null,
    bool HasTooltip = false)
{
    public static MatrixPresentation FromCard(CardDefinition card, SpecLibrary? library = null)
    {
        var diagram = card.Diagram;
        var height = CardChromeResolver.ResolveMatrixHeightPx(card, library);

        var tooltipFormat = InspectPresentationParser.ToTooltipFormat(card.Inspect);
        var tooltipSplit = card.Inspect?.Split ?? ", ";
        var tooltipLabel = card.Inspect?.Label;

        var xFormat = diagram.Properties.GetValueOrDefault("x_format") ?? "date.short";
        var yFormat = diagram.Properties.GetValueOrDefault("y_format") ?? "user.short";
        var colorScale = diagram.Properties.GetValueOrDefault("color_scale") ?? "heat";

        return new MatrixPresentation(
            height,
            DiagramBindings.Label(diagram, "x"),
            DiagramBindings.Label(diagram, "y"),
            DiagramBindings.Label(diagram, "value"),
            tooltipLabel,
            tooltipFormat,
            tooltipSplit,
            xFormat,
            yFormat,
            colorScale,
            card.Legend,
            card.Tooltip is not null);
    }

    public string? FormatLegendMin(double min, double max) =>
        FormatLegendTemplate(Legend?.MinLabel, min, max);

    public string? FormatLegendMax(double min, double max) =>
        FormatLegendTemplate(Legend?.MaxLabel, min, max);

    private static string? FormatLegendTemplate(string? template, double min, double max)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        return template
            .Replace("{min}", min.ToString("0"), StringComparison.Ordinal)
            .Replace("{max}", max.ToString("0"), StringComparison.Ordinal);
    }
}
