namespace DashSpec.Abstractions.Plugins;

/// <summary>Card-local presentation state (e.g. active diagram view).</summary>
public interface ICardViewState
{
    string? GetActiveView(string cardId);

    void SetActiveView(string cardId, string viewId);

    void Clear(string cardId);

    void ClearAll();
}
