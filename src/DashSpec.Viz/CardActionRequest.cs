namespace DashSpec.Viz;

public sealed record CardActionRequest(
    string CardId,
    string ActionId,
    IReadOnlyDictionary<string, string> Args);
