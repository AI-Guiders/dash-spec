namespace DashSpec.Core.Model;

/// <summary>How to present a tooltip (ADR-0029). Lives on inspect / show, not on tooltip entity.</summary>
public sealed record InspectPresentation(
    string? TooltipId = null,
    string? Label = null,
    string Format = "inline",
    string Split = ", ");
