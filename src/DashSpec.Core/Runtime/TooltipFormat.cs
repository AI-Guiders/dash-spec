namespace DashSpec.Core.Runtime;

public enum TooltipFormat
{
    Inline,
    List,
}

public static class TooltipFormatParser
{
    public static TooltipFormat Parse(string? raw, TooltipFormat fallback = TooltipFormat.Inline) =>
        raw?.Trim().ToLowerInvariant() switch
        {
            "list" or "bullets" or "ul" => TooltipFormat.List,
            "inline" or "line" or "text" => TooltipFormat.Inline,
            null or "" => fallback,
            _ => fallback,
        };
}
