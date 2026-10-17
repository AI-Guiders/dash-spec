using DashSpec.Core.Parsing;
using Microsoft.FSharp.Core;

namespace DashSpec.Execution.Runtime;

/// <summary>Resolves <c>diagram &lt;library-preset&gt;</c> into effective kind, bindings, and chrome.</summary>
public static class CardDiagramResolver
{
    public static ResolvedCardView Resolve(CardDefinition card, SpecLibrary? library)
    {
        if (string.IsNullOrWhiteSpace(card.Diagram.UsePreset?.Value))
        {
            return new ResolvedCardView(card, RenderPluginId: null);
        }

        var presetId = card.Diagram.UsePreset!.Value;
        var preset = library?.TryGetDiagram(presetId)
            ?? throw new InvalidOperationException(
                $"Card '{card.Id}': diagram preset '{presetId}' was not found in @diagramlibrary.");

        var diagramProps = new Dictionary<string, string>(preset.Properties, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in card.Diagram.Properties)
        {
            diagramProps[key] = value;
        }

        var diagram = new DiagramDefinition(preset.Kind, diagramProps, FSharpOption<string>.None);
        var presentation = MergePresentation(preset.PresentationPreset, card.Presentation?.Value);
        var seriesTransform = MergeSeriesTransform(preset.SeriesTransformPreset, card.SeriesTransform?.Value);
        var inspect = InspectPresentationParser.Merge(preset.Inspect, card.Inspect?.Value);
        var tooltip = card.Tooltip?.Value ?? preset.Tooltip;

        var resolvedCard = card
            .WithDiagram(diagram)
            .WithPresentation(ToFSharpOption(presentation))
            .WithSeriesTransform(ToFSharpOption(seriesTransform))
            .WithInspect(ToFSharpOption(inspect))
            .WithTooltip(ToFSharpOption(tooltip));

        return new ResolvedCardView(resolvedCard, preset.Render);
    }

    public static string ResolveKind(CardDefinition card, SpecLibrary? library) =>
        Resolve(card, library).Card.Diagram.Kind;

    private static PresentationBlock? MergePresentation(
        string? presetName,
        PresentationBlock? cardBlock)
    {
        var presetBlock = string.IsNullOrWhiteSpace(presetName)
            ? null
            : new PresentationBlock(FSharpOption<string>.Some(presetName), new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        if (presetBlock is null)
        {
            return cardBlock;
        }

        if (cardBlock is null)
        {
            return presetBlock;
        }

        var use = cardBlock.UsePreset ?? presetBlock.UsePreset;
        var inline = new Dictionary<string, string>(presetBlock.Properties, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in cardBlock.Properties)
        {
            inline[key] = value;
        }

        return new PresentationBlock(use, inline);
    }

    private static SeriesTransformBlock? MergeSeriesTransform(
        string? presetName,
        SeriesTransformBlock? cardBlock)
    {
        var presetBlock = string.IsNullOrWhiteSpace(presetName)
            ? null
            : new SeriesTransformBlock(
                FSharpOption<string>.Some(presetName),
                FSharpOption<int>.None,
                FSharpOption<string>.None);

        if (presetBlock is null)
        {
            return cardBlock;
        }

        if (cardBlock is null)
        {
            return presetBlock;
        }

        var use = cardBlock.UsePreset ?? presetBlock.UsePreset;
        return new SeriesTransformBlock(use, cardBlock.Max, cardBlock.OtherLabel);
    }

    private static FSharpOption<T> ToFSharpOption<T>(T? value) where T : class =>
        value is null ? FSharpOption<T>.None : FSharpOption<T>.Some(value);
}

public sealed record ResolvedCardView(CardDefinition Card, string? RenderPluginId);
