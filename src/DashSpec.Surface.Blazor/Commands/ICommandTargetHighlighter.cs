#nullable enable

namespace DashSpec.Surface.Blazor.Commands;

public enum CommandTargetKind
{
    Filter,
    Card,
}

/// <summary>
/// Surface contract for command-mode target highlights (filters and cards).
/// Implemented by the commands plugin when loaded; defaults to <see cref="NullCommandTargetHighlighter"/>.
/// </summary>
public interface ICommandTargetHighlighter
{
    event Action? HighlightsChanged;

    bool IsFilterHighlighted(string filterName);

    bool IsCardHighlighted(string cardId);
}

public sealed class NullCommandTargetHighlighter : ICommandTargetHighlighter
{
    public event Action? HighlightsChanged
    {
        add { }
        remove { }
    }

    public bool IsFilterHighlighted(string filterName) => false;

    public bool IsCardHighlighted(string cardId) => false;
}
