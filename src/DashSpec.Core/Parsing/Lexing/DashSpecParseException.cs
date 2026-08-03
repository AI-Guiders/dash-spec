namespace DashSpec.Core.Parsing;

public sealed class DashSpecParseException : Exception
{
    public DashSpecParseException(string message, int? sourceOffset = null)
        : base(message)
    {
        SourceOffset = sourceOffset;
    }

    /// <summary>0-based character offset in source text, when known.</summary>
    public int? SourceOffset { get; }
}
