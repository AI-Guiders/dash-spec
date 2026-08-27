using DashSpec.Core.Model;
using DashSpec.Core.Runtime;

namespace DashSpec.Core.Parsing;

internal static class InspectPresentationParser
{
    public static InspectPresentation Parse(TokenReader reader, string context)
    {
        BlockSyntax.BeginBlock(reader);
        reader.SkipNewlines();

        string? tooltipId = null;
        string? label = null;
        string? format = null;
        string? split = null;

        while (!BlockSyntax.IsBlockEnd(reader, "inspect") && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (BlockSyntax.IsBlockEnd(reader, "inspect"))
            {
                break;
            }

            if (reader.TryKeyword("use"))
            {
                if (!reader.TryKeyword("tooltip"))
                {
                    throw new DashSpecParseException($"{context}: inspect 'use' requires 'tooltip <id>'.");
                }

                tooltipId = reader.ReadIdent();
                if (string.IsNullOrWhiteSpace(tooltipId))
                {
                    throw new DashSpecParseException($"{context}: inspect use tooltip requires an id.");
                }

                continue;
            }

            if (reader.TryKeyword("label"))
            {
                reader.Expect(TokenKind.Eq);
                label = reader.ReadString();
                continue;
            }

            if (reader.TryKeyword("as"))
            {
                var token = reader.ReadIdent();
                format = token?.ToLowerInvariant() switch
                {
                    "list" or "bullets" or "ul" => "list",
                    "inline" or "line" or "text" => "inline",
                    _ => throw new DashSpecParseException(
                        $"{context}: inspect as must be list or inline; got '{token}'."),
                };
                continue;
            }

            if (reader.TryKeyword("split"))
            {
                reader.Expect(TokenKind.Eq);
                split = reader.ReadString();
                continue;
            }

            throw reader.Unexpected();
        }

        BlockSyntax.ExpectBlockEnd(reader, "inspect");

        if (string.IsNullOrWhiteSpace(tooltipId))
        {
            throw new DashSpecParseException($"{context}: inspect requires 'use tooltip <id>'.");
        }

        var resolvedFormat = format
            ?? (!string.IsNullOrWhiteSpace(split) ? "list" : "inline");

        return new InspectPresentation(
            tooltipId,
            label,
            resolvedFormat,
            string.IsNullOrWhiteSpace(split) ? ", " : split);
    }

    public static InspectPresentation? Merge(InspectPresentation? left, InspectPresentation? right)
    {
        if (right is null)
        {
            return left;
        }

        if (left is null)
        {
            return right;
        }

        return new InspectPresentation(
            right.TooltipId ?? left.TooltipId,
            right.Label ?? left.Label,
            right.Format ?? left.Format,
            right.Split != ", " ? right.Split : left.Split);
    }

    public static TooltipFormat ToTooltipFormat(InspectPresentation? inspect) =>
        TooltipFormatParser.Parse(inspect?.Format, TooltipFormat.Inline);
}
