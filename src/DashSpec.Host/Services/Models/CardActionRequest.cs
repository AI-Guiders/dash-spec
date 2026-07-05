namespace DashSpec.Host.Services.Models;

public sealed record CardActionRequest(
    string CardId,
    string ActionId,
    IReadOnlyDictionary<string, string> Args);
